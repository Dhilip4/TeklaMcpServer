"""
Tekla Semantic Search Server (local MiniLM)
============================================
Uses teknovizier/minilm-tekla-attr-embed-v1 — fully offline, no API key needed.
~5ms per query vs ~1000ms with cloud APIs.

Build the vector DB first:
    py build_local_db.py

Resolution order for attributes:
  1. Exact/normalized match against config/semantic_overrides.json (instant)
  2. Local MiniLM embedding -> ChromaDB cosine search (~5ms)
"""

from __future__ import annotations

import json
import logging
import re
import sys
import time
from functools import lru_cache
from pathlib import Path

# Logging to stderr + file (stdout is reserved for MCP JSON-RPC)
LOG_FILE = Path(__file__).parent / "search_server.log"
logging.basicConfig(
    level=logging.INFO,
    format="[%(asctime)s] %(levelname)s %(message)s",
    datefmt="%H:%M:%S",
    handlers=[
        logging.StreamHandler(sys.stderr),
        logging.FileHandler(str(LOG_FILE), mode="a", encoding="utf-8"),
    ],
)
log = logging.getLogger("tekla-search")

import numpy as np
import chromadb
from sentence_transformers import SentenceTransformer

# Paths
DB_DIR         = Path(__file__).parent / "chroma_db"
CONFIG_DIR     = Path(__file__).parent.parent / "config"
OVERRIDES_FILE = CONFIG_DIR / "semantic_overrides.json"
MODEL_ID       = "teknovizier/minilm-tekla-attr-embed-v1"

# Confidence thresholds (cosine distance — lower = more similar)
SPREAD_THRESHOLD = 0.05   # min std dev of top-k distances for confident match
MIN_THRESHOLD    = 0.45   # max distance for confident match

STANDARD_PROPERTIES = {
    "NAME", "PROFILE", "MATERIAL", "CLASS", "FINISH", "PHASE",
    "WEIGHT", "LENGTH", "HEIGHT", "WIDTH",
    "START_X", "START_Y", "START_Z", "END_X", "END_Y", "END_Z",
}

# Lazy-loaded singletons
_model        = None
_chroma       = None
_query_cache  = {}

# Per-call diagnostics
_call_diag = {}


def _reset_diag():
    global _call_diag
    _call_diag = {
        "model_calls": 0,
        "model_ms": 0,
        "cache_hits": 0,
        "chroma_ms": 0,
        "override_hit": False,
        "total_ms": 0,
    }


def _get_model():
    global _model
    if _model is None:
        log.info("Loading model %s ...", MODEL_ID)
        t0 = time.perf_counter()
        _model = SentenceTransformer(MODEL_ID)
        log.info("Model loaded in %.0f ms", (time.perf_counter() - t0) * 1000)
    return _model


def _get_chroma():
    global _chroma
    if _chroma is None:
        log.info("Loading ChromaDB from %s ...", DB_DIR)
        t0 = time.perf_counter()
        _chroma = chromadb.PersistentClient(path=str(DB_DIR))
        log.info("ChromaDB loaded in %.0f ms", (time.perf_counter() - t0) * 1000)
    return _chroma


def _normalize(text):
    text = text.lower().replace("_", " ").replace("-", " ")
    return re.sub(r"\s+", " ", text).strip()


def _embed_query(query):
    global _call_diag
    normalized = _normalize(query)
    if normalized in _query_cache:
        _call_diag["cache_hits"] += 1
        return _query_cache[normalized]

    t0 = time.perf_counter()
    vec = _get_model().encode(normalized, normalize_embeddings=True)
    elapsed_ms = (time.perf_counter() - t0) * 1000
    embedding = vec.tolist()

    _call_diag["model_calls"] += 1
    _call_diag["model_ms"] += round(elapsed_ms)

    if len(_query_cache) > 500:
        del _query_cache[next(iter(_query_cache))]
    _query_cache[normalized] = embedding
    return embedding


@lru_cache(maxsize=1)
def _load_overrides():
    if OVERRIDES_FILE.exists():
        return json.loads(OVERRIDES_FILE.read_text(encoding="utf-8"))
    return {}


def _override_match(query):
    overrides = _load_overrides()
    normalized = _normalize(query)

    if normalized in overrides:
        return overrides[normalized]
    alt = normalized.replace("_", " ")
    if alt in overrides:
        return overrides[alt]

    query_tokens = set(re.findall(r"\w+", normalized))
    for key in sorted(overrides, key=len, reverse=True):
        key_tokens = set(re.findall(r"\w+", key))
        if len(key_tokens) >= 2 and key_tokens.issubset(query_tokens):
            return overrides[key]
    return None


def _determine_tool(attr_name):
    base = attr_name.split(".")[-1] if "." in attr_name else attr_name
    if base.upper() in STANDARD_PROPERTIES:
        return "GetElementsProperties / SetElementsProperties"
    if attr_name.startswith("CUSTOM.") or attr_name.startswith("comment"):
        return "GetUserProperty (UDA)"
    return "GetReportProperty (template attribute)"


def _search_collection(collection_name, query, n_results):
    chroma = _get_chroma()
    try:
        col = chroma.get_collection(collection_name)
    except Exception:
        return {"error": f"Collection '{collection_name}' not found — run build_local_db.py first."}

    embedding = _embed_query(query)

    t0 = time.perf_counter()
    results = col.query(
        query_embeddings=[embedding],
        n_results=n_results,
        include=["documents", "metadatas", "distances"],
    )
    _call_diag["chroma_ms"] += round((time.perf_counter() - t0) * 1000)

    distances = results["distances"][0]
    top_distance = distances[0] if distances else 999
    spread = float(np.std(distances)) if len(distances) > 1 else 1.0
    confident = spread > SPREAD_THRESHOLD and top_distance < MIN_THRESHOLD

    output = []
    for i in range(len(results["ids"][0])):
        entry = {
            "rank": i + 1,
            "id": results["ids"][0][i],
            "distance": round(distances[i], 4),
            **results["metadatas"][0][i],
        }
        output.append(entry)

    return {
        "query": query,
        "confident_match": confident,
        "top_result": output[0]["id"] if output else None,
        "results": output,
    }


def search_attributes(query, top_k=10):
    """Search Tekla template attributes by natural language."""
    _reset_diag()
    log.info("search_attributes: %r", query)
    t_start = time.perf_counter()

    override = _override_match(query)
    if override:
        tool = _determine_tool(override)
        total_ms = round((time.perf_counter() - t_start) * 1000)
        _call_diag["override_hit"] = True
        _call_diag["total_ms"] = total_ms
        return {
            "query": query,
            "confident_match": True,
            "match_source": "semantic_override",
            "top_result": override,
            "recommended_tool": tool,
            "results": [{
                "rank": 1, "id": override, "distance": 0.0,
                "name": override, "match_type": "override",
                "recommended_tool": tool,
            }],
            "_diagnostics": {**_call_diag},
        }

    result = _search_collection("tekla_attributes", query, top_k)
    if "error" in result:
        return result

    for r in result["results"]:
        r["recommended_tool"] = _determine_tool(r.get("name", r.get("id", "")))

    result["match_source"] = "local_embedding"
    if result.get("top_result"):
        result["recommended_tool"] = _determine_tool(result["top_result"])

    total_ms = round((time.perf_counter() - t_start) * 1000)
    _call_diag["total_ms"] = total_ms
    result["_diagnostics"] = {**_call_diag}
    log.info("search_attributes done -> %s in %d ms", result.get("top_result"), total_ms)
    return result


def search_api(query, top_k=10):
    """Search the Tekla Open API by natural language."""
    _reset_diag()
    log.info("search_api: %r", query)
    t_start = time.perf_counter()

    result = _search_collection("tekla_api", query, top_k)
    if "error" in result:
        return result

    result["match_source"] = "local_embedding"
    total_ms = round((time.perf_counter() - t_start) * 1000)
    _call_diag["total_ms"] = total_ms
    result["_diagnostics"] = {**_call_diag}
    log.info("search_api done -> %s in %d ms", result.get("top_result"), total_ms)
    return result
