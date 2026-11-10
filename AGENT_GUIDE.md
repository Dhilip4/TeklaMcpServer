# TeklaMcpServer — Agent Usage Guide

This guide teaches AI agents (and the humans supervising them) how to effectively use the 76+ tools exposed by TeklaMcpServer to automate Tekla Structures 2022 workflows.

---

## Core Principles

### 1. Always Verify Connection First
Before any operation, confirm Tekla is running and a model is open:
```
Agent: GetModelStatus()
→ "Success: Model connected. Project: PEB_WAREHOUSE"
```

### 2. Select First, Then Operate
Follow Tekla's own UX pattern — **select elements, then act on them**. This is how structural engineers already work.

```
Step 1: Select    →  SelectElementsByFilter(name: "COLUMN")
Step 2: Inspect   →  GetElementsProperties()
Step 3: Modify    →  SetElementsUDAs({...})
```

### 3. Always CommitChanges
Every create/modify/delete operation requires a commit to persist. This is a Tekla API requirement:
```
Agent: ModelBeam(profile: "HEA300", material: "S355JR", ...)
Agent: [Tekla commits internally within each tool]
```

> **Note:** In this server, each tool calls `CommitChanges()` internally. Unlike some implementations, you don't need a separate `CommitChanges` tool call.

### 4. All Coordinates in Millimeters
Tekla's API uses millimeters for everything. A 6-meter span = `6000` mm.

---

## Tool Reference by Workflow

### 🔍 Model Inspection

| Task | Tool | Example |
|------|------|---------|
| Check connection | `GetModelStatus()` | Verify Tekla is running |
| Get project info | `GetModelInfo()` | Project name, designer, path |
| List model views | `ListModelViews()` | See all available 3D views |
| Check numbering | `CheckNumberingStatus()` | Are part numbers up to date? |
| Holes & Bolts Summary | `GetHolesSummary()` | Summarize hole diameters (e.g. M20 Dia 26mm: 774 NOS) on selected baseplates |
| Baseplate Anchor Templates | `GetBasePlateTemplates()` | Group base plates into templates (T1 = 129 NOS, T2 = 4 NOS) based on Pitch x Gauge |
| Inspect selected parts | `GetElementsProperties()` | Profiles, materials, weights, coordinates, and attached holes |

**Example conversation — Baseplate Anchor Bolt Templates (T1, T2...):**
```
You:    Prepare the template for base plate anchor holes (e.g. T1 = __ NOS, T2 = __ NOS).
Agent:  GetBasePlateTemplates()
→ 📐 BASE PLATE ANCHOR BOLT TEMPLATES REPORT
  Total Baseplates Inspected: 133
  Unique Template Patterns: 2

================================================================================
📋 TEMPLATE QUANTITIES SUMMARY (SITE ERECTION / FABRICATION)
================================================================================
  👉 T1 = 129 NOS  [M20 | Hole Dia 26mm | 2*100mm Pitch × 100mm Gauge (6 holes)]
  👉 T2 = 4 NOS    [M24 | Hole Dia 30mm | 2*100mm Pitch × 100mm Gauge (6 holes)]
================================================================================
```

**Example conversation — Checking hole diameters on selected baseplates:**
```
You:    I have selected some baseplates in the model. Can you get me the list of hole diameters? Example: M20 Dia 200 NOS?
Agent:  GetHolesSummary()
→ 🔩 HOLES & BOLTS SUMMARY:
  Selected Objects: 133 (Parts: 133, Direct Bolts: 0)
  Parts with Holes: 133
  Unique Bolt/Hole Groups: 133

📋 BREAKDOWN BY HOLE DIAMETER & BOLT SIZE:
  • M20 (Dia 26 mm, Tol 6 mm, HOLE_TYPE_SLOTTED) ➔ 774 NOS
  • M24 (Dia 30 mm, Tol 6 mm, HOLE_TYPE_SLOTTED) ➔ 24 NOS

  👉 GRAND TOTAL HOLES: 798 NOS
```

---

### 🏗️ Creating Steel Members

| Task | Tool | Key Parameters |
|------|------|----------------|
| Create beam/column | `ModelBeam()` | profile, material, start/end XYZ |
| Create curved member | `ModelPolyBeam()` | profile, material, semicolon-separated points |
| Create plate | `ModelContourPlate()` | profile (thickness), material, corner points |
| Create grid | `CreateGrid()` | X/Y/Z offsets and labels |

**Example — Create a portal frame column:**
```
You:    Create an HEA300 column at origin, 6 meters tall, S355 steel
Agent:  ModelBeam(
          profile: "HEA300",
          material: "S355JR",
          x1: 0, y1: 0, z1: 0,
          x2: 0, y2: 0, z2: 6000
        )
→ ✅ Beam 'HEA300' modeled. GUID: abc123-def456-...
```

**Example — Create a structural grid:**
```
You:    Create a grid: 4 bays at 6m in X, 2 bays at 8m in Y
Agent:  CreateGrid(
          xOffsets: "0 6000 12000 18000 24000",
          yOffsets: "0 8000 16000",
          zOffsets: "0",
          xLabels: "A B C D E",
          yLabels: "1 2 3",
          zLabels: "GL"
        )
→ ✅ Structural Grid generated successfully.
```

---

### 🔩 Connections & Detailing

| Task | Tool | Key Parameters |
|------|------|----------------|
| Add bolts | `AddBolts()` | primaryGuid, secondaryGuid, boltSize, boltStandard |
| Add bolt circle | `AddBoltCircle()` | + numberOfBolts, diameter |
| Add weld | `AddWeld()` | primaryGuid, secondaryGuid, weldSize |
| Add connection | `AddConnection()` | connectionName, connectionNumber, attributeFile |
| Cut plane | `AddCutPlane()` | guid, plane origin + normal |
| Fitting | `AddFitting()` | guid, plane origin + normal |
| Boolean cut | `ApplyPartCut()` | targetGuid, cuttingPartGuid |

**Example — Apply a system connection:**
```
You:    Apply an eaves haunch (macro 102) between column ABC and rafter DEF
Agent:  AddConnection(
          primaryGuid: "ABC...",
          secondaryGuid: "DEF...",
          connectionName: "Eaves haunch",
          connectionNumber: 102,
          attributeFile: "standard"
        )
→ ✅ Connection 'Eaves haunch' (#102) added.
```

---

### 🔧 Selection & Properties

| Task | Tool | Key Parameters |
|------|------|----------------|
| Select by GUID | `SelectElementsByGuid()` | guids array |
| Select by filter | `SelectElementsByFilter()` | name, profile, material, phase |
| Read properties | `GetElementsProperties()` | (operates on selection) |
| Set UDAs | `SetElementsUDAs()` | attributes dictionary |
| Read UDAs | `GetAllElementsUDAs()` | (operates on selection) |
| Find similar | `GetSimilarParts()` | guid |

**Example — Find and modify all IPE400 beams:**
```
You:    Change all IPE400 beams to phase 3
Agent:  1. SelectElementsByFilter(profile: "IPE400")
        → ✅ Selected 24 elements matching filter.
        
        2. ManagePhases(phaseNumber: 3, phaseName: "Phase 3 - Secondary Steel")
        → ✅ Assigned 24 objects to Phase 3.
```

---

### 📐 Drawing Workflows

This is where agent automation delivers the most value. Here's the typical flow:

#### Creating Drawings
```
Step 1: Select parts in model    →  SelectElementsByFilter(name: "COLUMN")
Step 2: Create drawings          →  CreateAssemblyDrawing()
Step 3: (Optional) Create GA     →  CreateGADrawing(name: "FRAME AT GL-8",
                                        viewNameFilter: "FRAME CROSS SECTION AT G.L - 8")
```

#### Working with an Open Drawing
```
Step 1: Open                     →  OpenDrawing(mark: "A1")
Step 2: List views               →  GetDrawingViews()
Step 3: Add marks                →  AddPartMarks()
Step 4: Add bolt marks           →  AddBoltMarks()
Step 5: Add dimensions           →  AddOverallDimensions()
Step 6: Add text note            →  AddTextToDrawing(text: "FOR APPROVAL", x: 100, y: 50)
Step 7: Arrange marks            →  ArrangeMarksNoCollisions()
Step 8: Set scale                →  SetDrawingScale(scale: 25)
Step 9: Fit to sheet             →  FitViewsToSheet()
Step 10: Close                   →  CloseActiveDrawing(save: true)
```

#### Drawing Status & Lifecycle
```
Get all statuses                 →  GetDrawingStatusReport()
Lock a drawing                   →  SetDrawingLifecycle(mark: "A1", isLocked: true, ...)
Update revision                  →  UpdateRevision(drawingMark: "A1", revisionMark: "B", ...)
Export to PDF                    →  ExportToPdf(mark: "A1")
```

**Example — Complete drawing automation:**
```
You:    Create an assembly drawing for every column, add marks and dimensions,
        then export all to PDF.

Agent:  1. SelectElementsByFilter(name: "COLUMN")
        → ✅ Selected 14 elements.
        
        2. CreateAssemblyDrawing()
        → ✅ Created 14 assembly drawings.
        
        3. ListDrawings()
        → Shows all 14 new assembly drawings with marks A1-A14
        
        4. For each drawing (A1 through A14):
           a. OpenDrawing(mark: "A1")
           b. AddPartMarks()
           c. AddBoltMarks()
           d. AddOverallDimensions()
           e. ArrangeMarksNoCollisions()
           f. CloseActiveDrawing(save: true)
           g. ExportToPdf(mark: "A1")
        
        → ✅ 14 drawings created, detailed, and exported.
```

---

### 📊 Exports & Reports

| Task | Tool | Key Parameters |
|------|------|----------------|
| Generate report | `GenerateReport()` | reportTemplateName, outputFileName |
| Display report | `DisplayReport()` | reportFilePath |
| Export NC/DSTV | `ExportNC()` | (operates on selection) |
| Export MIS | `CreateMISExport()` | exportType (DSTV/KISS/EJE), outputPath |
| Export web model | `SaveAsWebModel()` | outputPath |

---

### 🏗️ Concrete & Reinforcement

| Task | Tool | Key Parameters |
|------|------|----------------|
| Add single rebar | `AddSingleRebar()` | partGuid, size, grade, start/end XYZ |
| Add rebar group | `AddRebarGroup()` | partGuid, size, grade, spacing, start/end XYZ |
| Calculate pour units | `CalculatePourUnits()` | (no params) |

---

### 🔧 Spatial Operations

| Task | Tool | Key Parameters |
|------|------|----------------|
| Move object | `MoveObject()` | guid, dX, dY, dZ |
| Copy object | `CopyObject()` | guid, dX, dY, dZ (returns new GUID) |
| Split beam | `SplitBeam()` | guid, split point XYZ |
| Combine beams | `CombineBeams()` | guid1, guid2 |

---

### 📏 Construction Aids

| Task | Tool |
|------|------|
| Reference line | `AddControlLine()` |
| Reference plane | `AddControlPlane()` |
| Reference circle | `AddControlCircle()` |
| Reference point | `AddControlPoint()` |
| Base point (BIM) | `AddBasePoint()` |

---

### 🤖 Macro Execution

| Task | Tool | Key Parameters |
|------|------|----------------|
| Run macro | `RunTeklaMacro()` | macroName (e.g., `"drawings\ADD-PART.cs"`) |
| Check if running | `IsMacroRunning()` | (no params) |
| Status bar message | `DisplayPrompt()` | message string |

---

## Advanced Agent Strategies

### Strategy 1: Smart Drawing Cloning (Standalone Module)

For repetitive assemblies, use the fingerprint engine to clone drawings from similar assemblies:

```
Step 1: Build fingerprint DB    →  BuildFingerprintsFromOpenModel()
Step 2: Find matches            →  FindTop3MatchingDrawings(targetGuid: "...")
Step 3: Clone from best match   →  CloneDrawingFromMatch(targetGuid: "...", matchNumber: 1)
```

### Strategy 2: Batch Model Scanning

Discover all Tekla models on the system:

```
Step 1: Scan drives             →  ScanForTeklaModels(drives: "C,D")
Step 2: List results            →  ListModelRegistry()
```

### Strategy 3: GA Drawing from Named View

Create a General Arrangement drawing from an existing named model view:

```
Agent:  CreateGADrawing(
          name: "FRAME CROSS SECTION AT G.L - 8",
          viewNameFilter: "FRAME CROSS SECTION AT G.L - 8",
          viewDepthUp: 650,
          viewDepthDown: 650
        )
```

### Strategy 4: Multi-Step Connection Workflow

```
1. SelectElementsByFilter(name: "COLUMN")      → Get all columns
2. GetElementsProperties()                      → Read GUIDs and positions
3. SelectElementsByFilter(name: "RAFTER")       → Get all rafters  
4. GetElementsProperties()                      → Read GUIDs and positions
5. For each column-rafter pair:
   AddConnection(primaryGuid, secondaryGuid,
     connectionName: "Eaves haunch",
     connectionNumber: 102)
6. GetModelInfo()                               → Verify changes
```

---

## Error Handling Guide

All tools return string responses with emoji prefixes:

| Prefix | Meaning | Agent Action |
|--------|---------|-------------|
| `✅` | Success | Continue workflow |
| `❌` | Expected failure | Check parameters, retry with corrections |
| `⚠️` | Error/exception | Report to user, may need manual intervention |
| `⏳` | In progress | Wait and check again (macros) |

**Common errors and fixes:**

| Error | Cause | Fix |
|-------|-------|-----|
| `"❌ No active drawing open."` | No drawing is opened in Tekla | Call `OpenDrawing(mark)` first |
| `"❌ Part not found."` | Invalid GUID | Re-select parts, get fresh GUIDs |
| `"❌ No active model connection."` | Tekla not running or model not open | Start Tekla and open a model |
| `"⚠️ Failed to model beam."` | Invalid profile or material string | Verify against Tekla catalog |

---

## Tool Count Summary

| Category | File | Tools |
|----------|------|-------|
| Model Operations | `ModelTools.cs` | 26 |
| Drawing Lifecycle | `DrawingTools.cs` | 18 |
| Drawing Details | `DrawingDetailTools.cs` | 16 |
| Selection & Query | `SelectionTools.cs` | 7 |
| Reports & Export | `ExportTools.cs` | 4 |
| Macros & UI | `MacroTools.cs` | 3 |
| Views & Grids | `ViewTools.cs` | 2 |
| Phases | `PhaseTools.cs` | 1 |
| **Total (modular)** | | **77** |
| Fingerprint (standalone) | `FingerprintEngine.cs` | 3 |
| Scanner (standalone) | `ModelScanner.cs` | 2 |
| Smart Clone (standalone) | `SmartClone.cs` | 2 |
| **Grand Total** | | **84** |
