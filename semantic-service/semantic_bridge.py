"""
Semantic Bridge — JSON stdin/stdout subprocess for the C# MCP server.
=====================================================================
NOT an MCP server. Reads one JSON object per line from stdin,
writes one JSON response per line to stdout. Logs to stderr.

Protocol:
  → {"method": "search_attributes", "query": "flange thickness", "top_k": 10}
  ← {"result": {...}}

  → {"method": "search_api", "query": "create plate", "top_k": 10}
  ← {"result": {...}}

  → {"method": "ping"}
  ← {"result": "pong"}

  → {"method": "shutdown"}
  ← (process exits)

Usage (from C# server):
  py semantic_bridge.py
"""

from __future__ import annotations

import json
import logging
import sys
from pathlib import Path

# Logging to stderr only (stdout is the JSON pipe to C#)
logging.basicConfig(
    level=logging.INFO,
    format="[%(asctime)s] %(levelname)s %(message)s",
    datefmt="%H:%M:%S",
    handlers=[
        logging.StreamHandler(sys.stderr),
        logging.FileHandler(
            str(Path(__file__).parent / "semantic_bridge.log"),
            mode="a", encoding="utf-8"
        ),
    ],
)
log = logging.getLogger("semantic-bridge")

# Import all the search logic from search_server
sys.path.insert(0, str(Path(__file__).parent))
from search_server import search_attributes, search_api  # noqa: E402


def handle_request(req: dict) -> dict:
    """Route a JSON request to the appropriate handler."""
    method = req.get("method", "")
    query = req.get("query", "")
    top_k = req.get("top_k", 10)

    if method == "ping":
        return {"result": "pong"}

    if method == "search_attributes":
        if not query:
            return {"error": "Missing 'query' parameter"}
        result = search_attributes(query=query, top_k=top_k)
        if isinstance(result, dict) and result.get("error"):
            return {"error": result["error"]}
        return {"result": result}

    if method == "search_api":
        if not query:
            return {"error": "Missing 'query' parameter"}
        result = search_api(query=query, top_k=top_k)
        if isinstance(result, dict) and result.get("error"):
            return {"error": result["error"]}
        return {"result": result}

    if method == "shutdown":
        log.info("Shutdown requested")
        return {"result": "shutting_down"}

    return {"error": f"Unknown method: {method}"}


def main():
    log.info("Semantic bridge started, waiting for requests on stdin...")

    # Signal readiness
    sys.stdout.write(json.dumps({"ready": True}) + "\n")
    sys.stdout.flush()

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue

        try:
            req = json.loads(line)
        except json.JSONDecodeError as e:
            resp = {"error": f"Invalid JSON: {e}"}
            sys.stdout.write(json.dumps(resp) + "\n")
            sys.stdout.flush()
            continue

        log.info("Request: %s", req.get("method", "?"))

        if req.get("method") == "shutdown":
            resp = handle_request(req)
            sys.stdout.write(json.dumps(resp) + "\n")
            sys.stdout.flush()
            break

        try:
            resp = handle_request(req)
        except Exception as e:
            log.exception("Error handling request")
            resp = {"error": str(e)}

        sys.stdout.write(json.dumps(resp) + "\n")
        sys.stdout.flush()

    log.info("Semantic bridge exiting.")


if __name__ == "__main__":
    main()
