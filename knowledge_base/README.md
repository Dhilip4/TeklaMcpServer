# Tekla RAG Knowledge Base

This is the local knowledge base for the Tekla MCP Server's RAG (Retrieval-Augmented Generation) system.

## Categories

| Folder | Purpose | Examples |
|--------|---------|----------|
| `api_reference/` | Tekla Open API classes, methods, patterns | API quick-ref, class docs, code samples |
| `engineering_standards/` | Structural design codes & specs | Eurocode, AISC, IS800, bolt tables |
| `company_standards/` | Your company's detailing & fabrication rules | Edge distances, weld sizing, naming conventions |
| `macros_and_code/` | Reusable C# snippets, macros, patterns | Drawing macros, model macros, API tricks |
| `project_templates/` | Standard assemblies, connection details | Typical frames, base plates, handrails |
| `model_checker_rules/` | Rules for automated BIM model validation | Clash rules, naming rules, property checks |

## How to Add Knowledge

1. Create a `.md` (Markdown) or `.txt` file in the appropriate category folder.
2. Use clear headings and bullet points — the search engine indexes every line.
3. Restart the MCP server to re-index.

## Future Upgrades

- **Vector Search**: Add an embeddings API key to enable semantic search (understand meaning, not just keywords).
- **PDF Ingestion**: Auto-extract text from engineering PDFs.
- **Live Model Indexing**: Dump Tekla model properties into searchable documents.
