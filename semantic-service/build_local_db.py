"""
Build ChromaDB vector database using local MiniLM model.
============================================================
Uses teknovizier/minilm-tekla-attr-embed-v1 (384-dim, ~33M params).
No API key needed. Runs fully offline after first model download.

Collections built:
  tekla_attributes  - Template attributes from parsed_attributes.json
  tekla_api         - API members from knowledge_base data (if available)

Usage:
    py build_local_db.py
"""

from __future__ import annotations

import json
import re
import time
from pathlib import Path

import chromadb
from sentence_transformers import SentenceTransformer

# Paths
BASE_DIR    = Path(__file__).parent
DB_DIR      = BASE_DIR / "chroma_db"
ATTRS_FILE  = BASE_DIR / "parsed_attributes.json"
# Try multiple locations for API members
API_FILE_CANDIDATES = [
    BASE_DIR.parent / "Knowledge" / "api-members.txt",
    BASE_DIR / "api-members.txt",
]
MODEL_ID    = "teknovizier/minilm-tekla-attr-embed-v1"
BATCH_SIZE  = 512


def normalize(text: str) -> str:
    """Lowercase, replace underscores/hyphens with spaces, strip extras."""
    text = text.lower().replace("_", " ").replace("-", " ")
    return re.sub(r"\s+", " ", text).strip()


def embed_batch(model: SentenceTransformer, texts: list) -> list:
    vecs = model.encode(texts, batch_size=BATCH_SIZE, show_progress_bar=False, normalize_embeddings=True)
    return [v.tolist() for v in vecs]


def build_attributes(model: SentenceTransformer, client: chromadb.PersistentClient):
    print("\n[1/2] Building tekla_attributes collection...")

    if not ATTRS_FILE.exists():
        print(f"  WARNING: {ATTRS_FILE} not found — skipping attribute build.")
        print(f"  Copy parsed_attributes.json from the reference repo to {ATTRS_FILE}")
        return

    try:
        client.delete_collection("tekla_attributes")
    except Exception:
        pass
    col = client.create_collection("tekla_attributes", metadata={"hnsw:space": "cosine"})

    data = json.loads(ATTRS_FILE.read_text(encoding="utf-8"))
    print(f"  {len(data)} attributes loaded")

    ids, texts, metas = [], [], []
    for item in data:
        ids.append(item["id"])
        texts.append(normalize(item["text"]))
        metas.append(item["metadata"])

    t0 = time.time()
    total = len(ids)
    for i in range(0, total, BATCH_SIZE):
        batch_ids   = ids[i:i+BATCH_SIZE]
        batch_texts = texts[i:i+BATCH_SIZE]
        batch_metas = metas[i:i+BATCH_SIZE]
        embeddings  = embed_batch(model, batch_texts)
        col.add(ids=batch_ids, embeddings=embeddings, documents=batch_texts, metadatas=batch_metas)
        done = min(i + BATCH_SIZE, total)
        print(f"  {done}/{total} ({done*100//total}%)", end="\r", flush=True)

    print(f"\n  Done in {time.time()-t0:.1f}s — {total} vectors at 384 dims")


def build_api(model: SentenceTransformer, client: chromadb.PersistentClient):
    print("\n[2/2] Building tekla_api collection...")

    # Find API members file
    api_file = None
    for candidate in API_FILE_CANDIDATES:
        if candidate.exists():
            api_file = candidate
            break

    if api_file is None:
        print(f"  WARNING: api-members.txt not found — skipping API build.")
        print(f"  Place api-members.txt in {BASE_DIR} or Knowledge/ directory")
        return

    try:
        client.delete_collection("tekla_api")
    except Exception:
        pass
    col = client.create_collection("tekla_api", metadata={"hnsw:space": "cosine"})

    ids, texts, metas = [], [], []
    seen_ids = set()

    for line in api_file.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        parts = line.split(" | ", 3)
        if len(parts) < 2:
            continue
        signature   = parts[0]
        member_type = parts[1] if len(parts) > 1 else ""
        description = parts[2] if len(parts) > 2 else ""

        # Deduplicate IDs
        uid = signature[:500]
        if uid in seen_ids:
            count = sum(1 for s in seen_ids if s.startswith(uid))
            uid = f"{uid}#{count}"
        seen_ids.add(uid)

        ids.append(uid)
        search_text = normalize(f"{signature} {member_type} {description}")
        texts.append(search_text)
        metas.append({
            "signature":   signature[:500],
            "member_type": member_type,
            "description": description[:500],
        })

    print(f"  {len(ids)} API members loaded")

    t0 = time.time()
    total = len(ids)
    for i in range(0, total, BATCH_SIZE):
        batch_ids   = ids[i:i+BATCH_SIZE]
        batch_texts = texts[i:i+BATCH_SIZE]
        batch_metas = metas[i:i+BATCH_SIZE]
        embeddings  = embed_batch(model, batch_texts)
        col.add(ids=batch_ids, embeddings=embeddings, documents=batch_texts, metadatas=batch_metas)
        done = min(i + BATCH_SIZE, total)
        print(f"  {done}/{total} ({done*100//total}%)", end="\r", flush=True)

    print(f"\n  Done in {time.time()-t0:.1f}s — {total} vectors at 384 dims")


def main():
    print(f"Loading model: {MODEL_ID}")
    t0 = time.time()
    model = SentenceTransformer(MODEL_ID)
    print(f"Model loaded in {time.time()-t0:.1f}s")

    print(f"Opening ChromaDB at: {DB_DIR}")
    client = chromadb.PersistentClient(path=str(DB_DIR))

    existing = [c.name for c in client.list_collections()]

    if "tekla_attributes" in existing:
        col = client.get_collection("tekla_attributes")
        print(f"\n[1/2] tekla_attributes already built ({col.count()} vectors) — skipping")
    else:
        build_attributes(model, client)

    # Always rebuild API
    build_api(model, client)

    print("\nAll done! ChromaDB is ready.")
    cols = client.list_collections()
    for c in cols:
        print(f"  {c.name}: {c.count()} vectors")


if __name__ == "__main__":
    main()
