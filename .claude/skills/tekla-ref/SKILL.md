---
name: tekla-ref
description: 'Tekla Open API reference for the TeklaMcpServer project (Tekla 2022). Use when: (1) looking up any Tekla API class, method, or property, (2) asking "does the API have a way to do X?", (3) working with model objects, drawing automation, macro execution, or bolt/weld operations. Invoke proactively when coding any Tekla-related feature.'
---

# Tekla Open API Reference

Project: TeklaMcpServer (C# 7.3, .NET Framework 4.8, Tekla Structures 2022)

## This Project's Architecture

```
TeklaMcpServer/
├── Program.cs           # Entry point: TeklaInit + MCP host
├── TeklaInit.cs         # AppDomain.AssemblyResolve → C:\TeklaStructures\2022.0\bin
├── Tools/               # [McpServerToolType] classes auto-discovered by MCP SDK
│   ├── ModelTools.cs    # Beams, plates, bolts, welds, cuts, rebar (26 tools)
│   ├── DrawingTools.cs  # Drawing lifecycle (18 tools)
│   ├── DrawingDetailTools.cs # Marks, dims, views (16 tools)
│   ├── SelectionTools.cs    # Select/filter/properties (7 tools)
│   ├── CatalogTools.cs      # ListProfiles/Materials/Bolts/Components (4 tools)
│   ├── CoreTools.cs         # Commit/Delete/Modify/Statistics (9 tools)
│   ├── MacroTools.cs        # Run/List macros (4 tools)
│   ├── ViewTools.cs         # Model views, grids (2 tools)
│   ├── ExportTools.cs       # Reports, NC, MIS (4 tools)
│   └── PhaseTools.cs        # Construction phases (1 tool)
├── Utils/
│   ├── TeklaConnectionGuard.cs  # Connection check helpers
│   ├── OperationLog.cs          # Batch operation logger
│   └── QueryFileHelper.cs      # Disk-backed query pipeline
├── config/                      # Runtime configuration (JSON)
└── macros/                      # Bundled Tekla macros (.cs Akit scripts)
```

## Quick Reference — Task → API

For task-specific patterns, read: [references/api-quickref.md](references/api-quickref.md)

## Critical Rules

1. **Register `AppDomain.AssemblyResolve` before any Tekla type** — JIT fails otherwise
2. **.NET Framework 4.8, x64** — matches Tekla 2022 runtime
3. **Fresh `new Model()` per tool** — never cache across calls
4. **`CommitChanges()` after Insert/Modify/Delete** — changes are not persisted otherwise
5. **All coordinates in millimeters** — Tekla API standard
6. **stdout reserved for MCP JSON-RPC** — log to stderr (`Console.Error`) only
7. **Profile/material strings must match catalog** — use `ListProfiles`/`ListMaterials` to verify
8. **C# 7.3** — no `is Type var` pattern matching, use `as` + null check

## Known Gotchas (Tekla 2022)

- `GetSolid()` → `null` for `ReferenceModelObject`. Only works for native `Part` subclasses.
- `Solid.MinimumPoint/MaximumPoint` is relative to the **current work plane** — switch to global first.
- `Operation.RunMacro()` searches `XS_MACRO_DIRECTORY` paths — bundled macros need to be copied there.
- Tekla caches DLLs per macro session — restart Tekla after deploying a new DLL.
- `DrawingHandler.SetActiveDrawing()` needs `Thread.Sleep(1000-2000)` for the drawing to fully load.
- Drawing attribute files (`.gd`, `.vi`) may have leading spaces in environment configs — use auto-detection.
- `DPMPrinterAttributes` is an internal API — use macro-based PDF export instead.

## Adding a New Tool

1. Pick the right `Tools/*.cs` file (or create new one)
2. Add `[McpServerTool, Description("...")]` to a `public static string` method
3. Class must have `[McpServerToolType]` attribute
4. Build with `dotnet build TeklaMcpServer.csproj`
5. Tool is auto-discovered — no registration needed

## Key Namespaces

```csharp
using Tekla.Structures.Model;           // Part, Beam, BoltGroup, Model, Operations
using Tekla.Structures.Model.UI;        // ModelObjectSelector
using Tekla.Structures.Drawing;         // DrawingHandler, Drawing, View, Mark
using Tekla.Structures.Drawing.UI;      // DrawingObjectSelector
using Tekla.Structures.Geometry3d;      // Point, Vector, CoordinateSystem
using Tekla.Structures.Catalogs;        // CatalogHandler, ProfileItem, MaterialItem
using Tekla.Structures;                 // TeklaStructuresSettings
using Tekla.Structures.Model.Operations; // Operation.RunMacro, Operation.IsMacroRunning
```
