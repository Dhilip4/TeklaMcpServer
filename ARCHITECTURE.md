# TeklaMcpServer — Project Architecture & Plugin Guide

An MCP (Model Context Protocol) server that connects AI agents (Claude, Gemini, etc.) to **Tekla Structures 2022**, enabling AI-driven structural steel modeling and drawing automation through natural language. Built with native C# .NET — no bridge layers, no WebSocket middleware.

This server exposes **76+ tools** that let an LLM interact with a running Tekla Structures instance. You describe what you want in natural language, and the agent translates that into Tekla API operations: creating beams, querying model data, generating and detailing drawings, and everything in between.

---

## Architecture

```
Claude / Gemini / Any MCP Client
    |
    +-- stdio (JSON-RPC) --> C# MCP Server (.NET Framework 4.8)     76+ tools
                                |
                                +-- Tekla Open API (.NET) --> Tekla Structures 2022 GUI
```

**Why C# and not Python?** Tekla's API is native .NET. Python-based servers use pythonnet as a bridge, adding a translation layer. This server calls the Tekla API directly — same language, same runtime, same type system.

---

## Project Structure

```
TeklaMcpServer/
├── Program.cs                      # Entry point (60 lines): assembly resolver + MCP host
├── TeklaInit.cs                    # Runtime assembly resolution for Tekla 2022
│
├── Tools/                          # 8 files, 76+ tools total
│   ├── ModelTools.cs               # Beams, plates, bolts, welds, cuts, rebar, controls (26 tools)
│   ├── DrawingTools.cs             # Drawing lifecycle: list, open, create, delete, update (18 tools)
│   ├── DrawingDetailTools.cs       # Marks, dimensions, text, DWG, section/detail views (16 tools)
│   ├── SelectionTools.cs           # Select by filter/GUID, properties, UDAs (7 tools)
│   ├── MacroTools.cs               # Run/check macros, display prompt (3 tools)
│   ├── ViewTools.cs                # List model views, create grids (2 tools)
│   ├── ExportTools.cs              # Reports, NC files, MIS export (4 tools)
│   └── PhaseTools.cs               # Construction phase management (1 tool)
│
├── Utils/                          # Shared infrastructure
│   ├── TeklaConnectionGuard.cs     # Connection check + error formatting
│   ├── OperationLog.cs             # Thread-safe batch operation logger
│   └── QueryFileHelper.cs          # Disk-backed query pipeline (JSON snapshots)
│
├── config/                         # Externalized configuration
│   ├── settings.json               # Tekla version, bin path, log level
│   ├── components.json             # Steel connection component definitions
│   └── element_types.json          # Tekla class → element type mappings
│
├── FingerprintEngine.cs            # [Standalone] Drawing fingerprint & clone matching
├── ModelScanner.cs                 # [Standalone] Drive scanner for Tekla models
├── SmartClone.cs                   # [Standalone] Smart drawing clone from fingerprint DB
│
└── TeklaMcpServer.csproj           # Build configuration
```

### File Roles

| File | Purpose |
|------|---------|
| `Program.cs` | Registers `TeklaInit` assembly resolver, creates MCP host, starts stdio transport |
| `TeklaInit.cs` | `AppDomain.AssemblyResolve` handler — finds Tekla DLLs from running instance at `C:\TeklaStructures\2022.0\bin` |
| `Tools/*.cs` | Each file is a `[McpServerToolType]` class with `[McpServerTool]` methods. Auto-discovered by the MCP SDK |
| `Utils/*.cs` | Shared helpers used across tool files (not exposed as MCP tools) |
| `config/*.json` | Runtime configuration — change without recompiling |

---

## Critical Constraints

These rules apply to ALL code in this project:

| Rule | Why |
|------|-----|
| **.NET Framework 4.8, x64 only** | Tekla 2022's native DLLs are 64-bit .NET 4.8 |
| **Register AssemblyResolve before any Tekla types** | JIT will fail if Tekla DLLs aren't resolvable when first referenced |
| **Tekla must be running** | The server connects to an active Tekla GUI via .NET Remoting |
| **Fresh `new Model()` per tool call** | Tekla Model instances must not be cached across invocations |
| **Always `CommitChanges()` after mutations** | Creates/modifies/deletes aren't persisted until committed |
| **All coordinates in millimeters** | Tekla API uses mm for all dimensions |
| **stdout is reserved for MCP JSON-RPC** | Log to `Console.Error` (stderr) only |
| **Profile/material strings must match catalog** | Invalid strings cause silent failures |

---

## How to Build

```powershell
# Using the local .NET SDK
& "C:\Users\dhilip.kumar\AppData\Local\Microsoft\dotnet\dotnet.exe" build TeklaMcpServer.csproj

# Output binary
bin\Debug\net48\TeklaMcpServer.exe
```

The `.csproj` sets `Private=false` on all Tekla references — they are resolved at runtime from `C:\TeklaStructures\2022.0\bin` via `TeklaInit.cs`, not copied to output.

---

## How to Configure MCP Client

**Gemini CLI** — add to your MCP config:

```json
{
  "mcpServers": {
    "tekla-2022": {
      "type": "stdio",
      "command": "C:\\Users\\dhilip.kumar\\TeklaMcpServer\\bin\\Debug\\net48\\TeklaMcpServer.exe",
      "args": [],
      "env": {}
    }
  }
}
```

---

## How to Create a New Tool (Plugin Guide)

Adding a new tool to this server takes **3 minutes**. Here's the complete process:

### Step 1: Choose the Right File

| If your tool does... | Add it to... |
|---------------------|-------------|
| 3D model operations (create/modify/delete parts) | `Tools/ModelTools.cs` |
| Drawing lifecycle (list/open/close/create) | `Tools/DrawingTools.cs` |
| Drawing annotations (marks/dims/text/graphics) | `Tools/DrawingDetailTools.cs` |
| Selection and querying | `Tools/SelectionTools.cs` |
| Macro execution | `Tools/MacroTools.cs` |
| Model views and grids | `Tools/ViewTools.cs` |
| Reports and exports | `Tools/ExportTools.cs` |
| Construction phases | `Tools/PhaseTools.cs` |
| **Something entirely new** | Create a new `Tools/YourNewTools.cs` |

### Step 2: Write the Tool Method

Every tool follows this exact pattern:

```csharp
[McpServerTool, Description("Human-readable description of what this tool does")]
public static string YourToolName(string requiredParam, double optionalParam = 0.0)
{
    try
    {
        // 1. Connect to model (fresh instance every time)
        var model = new TSM.Model();
        
        // 2. Do your Tekla API work
        // ...
        
        // 3. Commit if you modified anything
        model.CommitChanges();
        
        // 4. Return success with relevant data
        return "✅ Operation completed successfully.";
    }
    catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
}
```

### Step 3: Creating a Brand New Tool File

If your tools don't fit any existing category:

```csharp
using System;
using System.ComponentModel;
using ModelContextProtocol.Server;
using TSM = Tekla.Structures.Model;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]  // ← Required: tells MCP SDK to scan this class
    public static class ClashCheckTools
    {
        [McpServerTool, Description("Run a clash check between all parts in the model")]
        public static string RunClashCheck()
        {
            try
            {
                // Your implementation here
                return "✅ Clash check completed.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }
    }
}
```

**That's it.** No registration, no configuration, no routing. The MCP SDK auto-discovers all `[McpServerToolType]` classes and `[McpServerTool]` methods at startup via `WithToolsFromAssembly()`.

### Step 4: Build and Test

```powershell
# Build
& "C:\Users\dhilip.kumar\AppData\Local\Microsoft\dotnet\dotnet.exe" build TeklaMcpServer.csproj

# Restart your MCP client to pick up the new tool
```

### Common Patterns

**Reading a part by GUID:**
```csharp
var model = new TSM.Model();
var part = model.SelectModelObject(model.GetIdentifierByGUID(guid)) as TSM.Part;
if (part == null) return "❌ Part not found.";
```

**Working with the active drawing:**
```csharp
var handler = new DrawingHandler();
var active = handler.GetActiveDrawing();
if (active == null) return "❌ No active drawing open.";
// ... do work ...
active.CommitChanges();
```

**Iterating selected objects:**
```csharp
var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
var selected = selector.GetSelectedObjects();
while (selected.MoveNext())
{
    if (selected.Current is TSM.Part part)
    {
        // process each part
    }
}
```

**Working with coordinate systems (for drawings):**
```csharp
var myModel = new TSM.Model();
TransformationPlane savePlane = myModel.GetWorkPlaneHandler().GetCurrentTransformationPlane();
myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane(view.DisplayCoordinateSystem));
// ... do work in view coordinates ...
myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(savePlane); // always restore!
```

### Checklist for New Tools

- [ ] Method is `public static string`
- [ ] Has `[McpServerTool]` attribute
- [ ] Has `[Description("...")]` with clear, agent-friendly description
- [ ] Class has `[McpServerToolType]` attribute
- [ ] Uses `try/catch` with `⚠️ Error:` prefix
- [ ] Returns `✅` on success, `❌` on expected failure
- [ ] Creates fresh `new Model()` (never cached)
- [ ] Calls `CommitChanges()` after any Insert/Modify/Delete
- [ ] Does NOT write to stdout (use `Console.Error` for logging)

---

## Configuration Reference

### `config/settings.json`
```json
{
  "teklaVersion": "2022.0",
  "teklaBinDir": "C:\\TeklaStructures\\2022.0\\bin",
  "logLevel": "Info"
}
```

### `config/components.json`
Steel connection component definitions. Add new components here without touching code:
```json
{
  "base_plate": { "number": 1004, "description": "Base plate connection" },
  "end_plate": { "number": 1042, "description": "End plate connection" }
}
```

### `config/element_types.json`
Maps Tekla internal class numbers to human-readable types:
```json
{
  "1": "Column",
  "2": "Beam",
  "3": "Brace"
}
```

---

## Standalone Modules

These files contain MCP tools but are maintained independently:

| File | Tools | Purpose |
|------|-------|---------|
| `FingerprintEngine.cs` | `BuildFingerprintsFromOpenModel`, `GetFingerprintDatabaseStatus`, `RemoveFingerprintsFromModel` | Index drawing fingerprints for clone matching |
| `ModelScanner.cs` | `ScanForTeklaModels`, `ListModelRegistry` | Scan drives for Tekla model folders |
| `SmartClone.cs` | `FindTop3MatchingDrawings`, `CloneDrawingFromMatch` | Clone drawings from fingerprint database |

These are excluded from the modular `Tools/` structure by design — they have their own state management (JSON databases in `AGENT_MEMORY/`) and may be refactored independently.
