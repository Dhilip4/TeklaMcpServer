# 🧠 Agent Memory & Error Logs

> **Purpose**: This file acts as the long-term memory for AI agents. When an AI agent encounters a failure executing an MCP tool in Tekla, or discovers a specific modeling quirk for your company's workflow, it MUST document it here.

## How AI Agents Should Use This File
1. **Read Before Modeling**: At the start of a session, the AI should briefly scan this file for any "Gotchas" related to the requested task.
2. **Log After Recovery**: If an operation fails and the AI successfully troubleshoots it, the AI must append the scenario and fix to this file.

---

## 🛑 Known Errors & Resolutions

### Example Error: Tool Failed to Insert Part Cut
* **Scenario**: Standard `ApplyPartCut` failed on a PEB Knee connection.
* **Reason**: Cutting part was smaller than the target part's bounding box, resulting in a non-manifold solid error in Tekla.
* **Fix/Rule**: Always ensure the `OperativePart` (cutting geometry) extends at least 100mm PAST the boundaries of the part being cut.

### Example Error: Drawing View Out of Bounds
* **Scenario**: `FitViewsToSheet` didn't scale properly.
* **Reason**: An invisible construction line was sticking thousands of millimeters outside the structure, expanding the bounding box.
* **Fix/Rule**: Delete or hide construction lines before creating assembly drawings.

---

> *(AI Agents: Append new learnings below this line using the format above)*

### Error: Tekla Macro Compiler CS1056 Unexpected character '$'
* **Scenario**: Executing a dynamic C# macro via `RunTeklaMacro` with string interpolation `$"..."`.
* **Reason**: Tekla Structures' built-in macro compiler (using legacy CodeDOM / Akit) only supports C# 5 language syntax.
* **Fix/Rule**: Never use C# 6+ features like `$"..."` or declaration pattern matching (`obj is Part p`) in macro files. Use `string.Format(...)`, string concatenation `+`, and explicit casts `Part p = obj as Part;`.

### Error: Ambiguous 'ModelObjectSelector' in Model Macros
* **Scenario**: Creating `new ModelObjectSelector()` inside a macro or code referencing `Tekla.Structures.Model` and `Tekla.Structures.Model.UI`.
* **Reason**: Both namespaces define `ModelObjectSelector`, but `Tekla.Structures.Model.ModelObjectSelector` has no public constructor and does not have `GetSelectedObjects()`.
* **Fix/Rule**: Always qualify `new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects()`.

### Discovery: Baseplate Holes & Anchor Bolt Clearance
* **Scenario**: User asks for hole diameters and counts on base plates (e.g., "M20 DIA 200 NOS").
* **Reason**: Base plate anchor bolt holes in Tekla are stored as `BoltGroup` objects on the plate with `Bolt = false` (hole only). Standard part report properties only report plate geometry, not hole breakdown.
* **Fix/Rule**: Use `part.GetBolts()`. The hole diameter is `bg.BoltSize + bg.Tolerance` (e.g., M20 with 6mm tolerance = 26mm diameter hole). Or call the dedicated tool `GetHolesSummary()`.

