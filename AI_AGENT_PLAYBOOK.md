# 🏗️ Tekla MCP AI Agent Playbook
## Master Instruction Guide for AI-Driven Structural Engineering

> **Version**: 1.0 — April 2026  
> **Tools Available**: 84 verified MCP tools  
> **Target**: Tekla Structures 2022  
> **Purpose**: This document teaches an AI agent how to plan, sequence, and execute Tekla automation tasks using all available MCP tools.

---

## 📋 TABLE OF CONTENTS

1. [Core Principles](#1-core-principles)
2. [Tool Reference (All 84 Tools)](#2-tool-reference)
3. [Workflow Patterns](#3-workflow-patterns)
4. [Complete Example: PEB Building 100m × 100m × 12m](#4-complete-example-peb-building)
5. [Common Task Recipes](#5-common-task-recipes)
6. [Decision Trees](#6-decision-trees)
7. [Error Handling & Recovery](#7-error-handling)
8. [Best Practices](#8-best-practices)
9. [Researching the API: The Semantic Service](#11-researching-the-api-the-semantic-service)

---

## 1. CORE PRINCIPLES

### 1.1 Always Start With Connection Check
Before ANY operation, verify Tekla is running and responding:
```
Step 1: GetModelStatus() → Confirm "Connected"
Step 2: GetModelInfo() → Get model name, path, units, and coordinate system
```

### 1.2 The GUID is Everything
- Every 3D model object has a unique GUID (Globally Unique Identifier).
- GUIDs are returned when you create objects (`ModelBeam`, `ModelContourPlate`, `CreateGrid`).
- **Always capture and store GUIDs** — you will need them for connections, welds, bolts, cuts, and copies.
- Use `SelectElementsByFilter()` to find existing objects when you don't have GUIDs.
- Use `GetElementsProperties()` to inspect any selected object.

### 1.3 Coordinate System
- Tekla uses millimeters (mm) as the default unit.
- The origin (0,0,0) is at the project base point.
- X = East/West, Y = North/South, Z = Up/Down (elevation).
- When users say "100m × 100m × 12m", convert to **100000mm × 100000mm × 12000mm**.

### 1.4 Commit Changes
- All model modifications require a `CommitChanges()` call (handled internally by each tool).
- Drawing modifications require `CommitChanges()` on the drawing object (also handled internally).

### 1.5 Sequential Dependency Rule
Tools that depend on each other MUST be called sequentially. For example:
- You must `ModelBeam()` before you can `AddConnection()` between beams.
- You must `CreateAssemblyDrawing()` before you can annotate it.
- You must `OpenDrawing()` before you can add marks, dimensions, or views.

---

## 2. TOOL REFERENCE

### 2.1 Model Infrastructure (3 tools)
| Tool | When to Use |
|------|-------------|
| `GetModelStatus` | First call in every session — verify Tekla is alive |
| `GetModelInfo` | Get project name, folder path, units, phase information |
| `DisplayPrompt` | Show status messages to the user in Tekla's status bar |

### 2.2 Element Selection & Inspection (7 tools)
| Tool | When to Use |
|------|-------------|
| `SelectElementsByGuid` | When you have specific GUID(s) to work with |
| `SelectElementsByFilter` | Find objects by type/profile/material/phase/name |
| `GetElementsProperties` | Read profile, material, position, GUID of selected objects |
| `GetAllElementsUDAs` | Read custom attributes (lot number, fab status, etc.) |
| `SetElementsUDAs` | Write custom attributes to selected objects |
| `ShowOnlySelected` | Isolate selected objects in the 3D view |
| `ZoomToSelection` | Frame the camera on selected objects |

### 2.3 3D Modeling — Structure Creation (6 tools)
| Tool | When to Use |
|------|-------------|
| `CreateGrid` | **ALWAYS FIRST** — establish the structural grid before anything else |
| `ModelBeam` | Create straight beams, columns, and braces |
| `ModelPolyBeam` | Create curved, haunched, or multi-point beams |
| `ModelContourPlate` | Create floor slabs, base plates, gusset plates |
| `CombineBeams` | Merge two collinear beams into one |
| `SplitBeam` | Split a beam at a point (for splice locations) |

### 2.4 3D Modeling — Connections & Detailing (7 tools)
| Tool | When to Use |
|------|-------------|
| `AddConnection` | Apply system connections (end plates, clip angles, etc.) |
| `AddWeld` | Add physical 3D welds between parts |
| `AddBolts` | Create rectangular bolt patterns |
| `AddBoltCircle` | Create circular bolt patterns (flanges) |
| `AddCutPlane` | Cut a part with a plane (angled cuts) |
| `AddFitting` | Trim a part end to fit against another |
| `ApplyPartCut` | Boolean cut — notch or cope using another part as cutter |

### 2.5 3D Modeling — Spatial Operations (3 tools)
| Tool | When to Use |
|------|-------------|
| `MoveObject` | Relocate any part by a displacement vector |
| `CopyObject` | Duplicate a part at an offset — **returns new GUID** |
| `SplitBeam` | (also listed above) Break a beam into two at a point |

### 2.6 3D Modeling — Concrete & Rebar (4 tools)
| Tool | When to Use |
|------|-------------|
| `ModelContourPlate` | Create concrete slabs, walls, foundations |
| `AddSingleRebar` | Place one reinforcing bar precisely |
| `AddRebarGroup` | Create a rebar cage with spacing |
| `CalculatePourUnits` | Auto-calculate concrete pour boundaries |

### 2.7 3D Modeling — Construction Aids (4 tools)
| Tool | When to Use |
|------|-------------|
| `AddControlLine` | Reference/construction lines for layout |
| `AddControlPlane` | Reference planes for alignment |
| `AddControlCircle` | Reference circles (curved alignments) |
| `AddControlPoint` | Reference points (survey points, benchmarks) |

### 2.8 BIM Management (6 tools)
| Tool | When to Use |
|------|-------------|
| `ManagePhases` | Assign parts to construction phases (4D scheduling) |
| `InsertReferenceModel` | Import IFC/DWG overlays for coordination |
| `AddBasePoint` | Set geo-located coordinates for export |
| `AddSurfaceTreatment` | Apply paint/fireproofing/galvanizing |
| `CheckNumberingStatus` | Verify numbering is current before fabrication |
| `GetSimilarParts` | Find all parts with same mark (QC check) |

### 2.9 Reports & Export (6 tools)
| Tool | When to Use |
|------|-------------|
| `GenerateReport` | Run Tekla report templates (.xsr) |
| `DisplayReport` | Open a report in Tekla's viewer |
| `ExportNC` | Export DSTV files for CNC machinery |
| `ExportToPdf` | Export drawings as PDF |
| `SaveAsWebModel` | Export for Trimble Connect viewer |
| `CreateMISExport` | Export MIS format (DSTV/KISS/EJE/EPC) |

### 2.10 Drawing Discovery & Lifecycle (10 tools)
| Tool | When to Use |
|------|-------------|
| `ListDrawings` | See all drawings in the model |
| `GetSelectedDrawings` | See what's highlighted in drawing list |
| `OpenDrawing` | Open a drawing for editing |
| `CloseActiveDrawing` | Save and close current drawing |
| `DeleteDrawing` | Remove a drawing |
| `UpdateDrawing` | Sync drawing with model changes |
| `GetDrawingDetails` | Read drawing metadata |
| `GetDrawingStatusReport` | Audit all drawings (lock/freeze/issue) |
| `SetDrawingLifecycle` | Lock/Freeze/Issue a drawing |
| `RunTeklaMacro` | Execute any pre-compiled .cs macro |

### 2.11 Drawing Creation (4 tools)
| Tool | When to Use |
|------|-------------|
| `CreateGADrawing` | General arrangement (plan/elevation views) |
| `CreateAssemblyDrawing` | Assembly shop drawings |
| `CreateSinglePartDrawing` | Individual part detail drawings |
| `CreateCastUnitDrawing` | Concrete cast-unit drawings |

### 2.12 Drawing Annotation (10 tools)
| Tool | When to Use |
|------|-------------|
| `AddPartMarks` | Label all parts with marks |
| `AddBoltMarks` | Label all bolts |
| `AddWeldMarks` | Label all welds |
| `SelectPhaseInDrawing` | **NEW**: Select/Hide objects in drawing by 3D Phase |
| `AddOverallDimensions` | Auto-dimension bounding box |
| `PlaceControlDiagonals` | Diagonal check dimensions |
| `AddTextToDrawing` | Custom text notes |
| `ArrangeMarksNoCollisions` | Clean up overlapping marks |
| `AddDrawingLine` | Sketch markup lines |
| `AddDrawingCircle` | Sketch emphasis circles |
| `AddDrawingRectangle` | Sketch bounding boxes |

### 2.13 Drawing Views & Layout (8 tools)
| Tool | When to Use |
|------|-------------|
| `GetDrawingViews` | List existing views |
| `AddSectionView` | Create section cuts |
| `AddDetailView` | Create detail bubbles |
| `MoveView` | Reposition views on sheet |
| `SetDrawingScale` | Change view scale |
| `ModifyViewLabel` | Rename view labels |
| `FitViewsToSheet` | Auto-arrange views |
| `InsertDwgObject` | Import DWG reference on sheet |

### 2.14 Drawing Metadata (6 tools)
| Tool | When to Use |
|------|-------------|
| `GetDrawingUDA` | Read custom drawing attributes |
| `SetDrawingUDA` | Write custom drawing attributes |
| `CopyDrawingTitles` | Copy titles between drawings |
| `UpdateRevision` | Log revision history |
| `AddRevisionCloud` | Mark changed areas |
| `GetDrawingObjectCount` | Audit drawing complexity |

### 2.15 Macro & Status (2 tools)
| Tool | When to Use |
|------|-------------|
| `IsMacroRunning` | Check if a triggered macro is still executing |
| `RunTeklaMacro` | Execute any Tekla macro by name |

---

## 3. WORKFLOW PATTERNS

### Pattern A: "Build a Structure" (Modeling)
```
1. GetModelStatus()           → Verify connection
2. GetModelInfo()             → Confirm units and project
3. CreateGrid()               → Establish structural grid
4. ModelBeam() × N            → Place columns at grid intersections
5. ModelBeam() × N            → Place beams between columns
6. ModelBeam() × N            → Place braces (if needed)
7. ModelContourPlate() × N    → Place floor slabs / base plates
8. AddConnection() × N        → Apply connections at joints
9. ManagePhases()             → Assign to construction phases
10. CheckNumberingStatus()    → Verify numbering
```

### Pattern B: "Detail a Connection" (Detailing)
```
1. SelectElementsByGuid()     → Select the two parts
2. GetElementsProperties()    → Verify profiles and positions
3. ModelContourPlate()        → Create connection plates (if required)
4. AddWeld()                  → Weld plates to primary member
5. AddBolts() / AddBoltCircle() → Bolt secondary to plate
6. AddCutPlane() / AddFitting() → Trim/cope as needed
```

### Pattern C: "Produce Shop Drawings" (Documentation)
```
1. SelectElementsByFilter()   → Select assemblies by phase/name
2. CreateAssemblyDrawing()    → Generate drawings
3. OpenDrawing()              → Open each drawing
4. AddPartMarks()             → Label all parts
5. AddBoltMarks()             → Label all bolts
6. AddWeldMarks()             → Label all welds
7. AddOverallDimensions()     → Add dimensions
8. PlaceControlDiagonals()    → Add check dimensions
9. ArrangeMarksNoCollisions() → Clean up layout
10. FitViewsToSheet()         → Optimize view arrangement
11. CloseActiveDrawing()      → Save
12. ExportToPdf()             → Export PDF
```

### Pattern D: "Revision Workflow" (Change Management)
```
1. OpenDrawing()              → Open affected drawing
2. AddRevisionCloud()         → Mark changed area
3. UpdateRevision()           → Log revision (mark, date, description)
4. AddTextToDrawing()         → Add revision note
5. CloseActiveDrawing()       → Save
6. SetDrawingLifecycle()      → Issue the drawing
```

### Pattern E: "QC Audit" (Quality Control)
```
1. CheckNumberingStatus()     → Verify numbering
2. GetDrawingStatusReport()   → Audit all drawing states
3. SelectElementsByFilter()   → Select all steel parts
4. GetAllElementsUDAs()       → Check for missing attributes
5. GenerateReport()           → Run BOM report
6. DisplayReport()            → Show to user
```

### Pattern F: "Fabrication Export" (Production)
```
1. SelectElementsByFilter()   → Select parts by phase
2. ExportNC()                 → Generate DSTV files
3. CreateMISExport()          → Generate MIS files
4. GenerateReport()           → Generate cutting list
5. ExportToPdf()              → Export drawings as PDF
6. SaveAsWebModel()           → Publish to Trimble Connect
```

---

## 4. COMPLETE EXAMPLE: PEB BUILDING 100m × 100m × 12m

### User Request:
> "Create a Pre-Engineered Building: 100m × 100m, 12m eave height"

### AI Agent Execution Plan:

#### Phase 1: Project Setup
```
Step 1: GetModelStatus()
→ Result: "Connected to Tekla 2022"

Step 2: GetModelInfo()
→ Result: Model name, folder, units = mm

Step 3: DisplayPrompt("AI Agent: Starting PEB 100m x 100m x 12m build...")
```

#### Phase 2: Create Structural Grid
A typical PEB has:
- **X-direction**: Bay spacing of ~8m (13 bays for 100m = 12 × 8333mm)
- **Y-direction**: Frame spacing of ~8m (13 frames for 100m = 12 × 8333mm)
- **Z-direction**: Ground (0) + Eave height (12000mm)

```
Step 4: CreateGrid(
    xOffsets: "0.0 12*8333.0",
    yOffsets: "0.0 12*8333.0",
    zOffsets: "0.0 12000.0",
    xLabels: "A B C D E F G H I J K L M",
    yLabels: "1 2 3 4 5 6 7 8 9 10 11 12 13",
    zLabels: "GL EAVE"
)
```

#### Phase 3: Place Columns
Place columns at every grid intersection on the perimeter.
For a PEB, columns go on ALL intersections of the first and last Y-lines (exterior frames), plus interior frame lines.

**Perimeter columns (4 sides):**
```
Step 5-56: ModelBeam() × 52 calls
For each grid intersection on the perimeter:
    ModelBeam(
        profile: "HEA400",    ← or tapered welded I-section
        material: "S355JR",
        x1: gridX, y1: gridY, z1: 0,
        x2: gridX, y2: gridY, z2: 12000
    )
    → Store returned GUID for each column
```

**Strategy for perimeter detection:**
- Row 1 (Y=0): All 13 X positions → 13 columns
- Row 13 (Y=100000): All 13 X positions → 13 columns
- Rows 2-12 (interior): Only X=0 and X=100000 → 2 columns each × 11 rows = 22 columns
- Interior columns as needed for portal frames

#### Phase 4: Place Eave Beams (Rafters)
Connect column tops with beams at eave level:

```
Step 57+: ModelBeam() × N calls
For each portal frame:
    ModelBeam(
        profile: "IPE500",
        material: "S355JR",
        x1: 0, y1: frameY, z1: 12000,
        x2: 100000, y2: frameY, z2: 12000
    )
    → Store GUID
```

#### Phase 5: Place Purlins
Horizontal members between rafter beams at ~1500mm spacing on the roof:

```
For each purlin line (roof):
    ModelBeam(
        profile: "200Z20",     ← Z-purlin
        material: "S350GD",
        x1: purlinX, y1: 0, z1: 12000,
        x2: purlinX, y2: 100000, z2: 12000
    )
```

#### Phase 6: Place Wall Girts
Horizontal members on walls at ~1500mm vertical spacing:

```
For each girt line:
    ModelBeam(
        profile: "200Z20",
        material: "S350GD",
        x1: 0, y1: 0, z1: girtZ,
        x2: 100000, y2: 0, z2: girtZ
    )
```

#### Phase 7: Place Bracing
X-braces in end bays for lateral stability:

```
ModelBeam(profile: "L100X100X10", material: "S235JR",
    x1: 0, y1: 0, z1: 0,
    x2: 8333, y2: 0, z2: 12000)
ModelBeam(profile: "L100X100X10", material: "S235JR",
    x1: 8333, y1: 0, z1: 0,
    x2: 0, y2: 0, z2: 12000)
```

#### Phase 8: Place Base Plates
```
For each column base:
    ModelContourPlate(
        profile: "PL25",
        material: "S355JR",
        pointCoordinates: "x-300,y-300,0;x+300,y-300,0;x+300,y+300,0;x-300,y+300,0"
    )
```

#### Phase 9: Add Connections
```
For each column-to-rafter joint:
    AddConnection(
        primaryGuid: columnGUID,
        secondaryGuid: rafterGUID,
        connectionName: "Haunch",
        connectionNumber: 142      ← Tekla system component number
    )

For each column-to-base-plate:
    AddBolts(
        primaryGuid: basePlateGUID,
        secondaryGuid: columnGUID,
        boltSize: 24,
        boltStandard: "7990"
    )
```

#### Phase 10: Add Welds
```
For each plate-to-column connection:
    AddWeld(
        primaryGuid: columnGUID,
        secondaryGuid: plateGUID,
        weldSize: 8
    )
```

#### Phase 11: Phase Assignment
```
ManagePhases(phaseNumber: 1, phaseName: "Main Frames")
ManagePhases(phaseNumber: 2, phaseName: "Purlins & Girts")
ManagePhases(phaseNumber: 3, phaseName: "Bracing")
```

#### Phase 12: Generate Drawings
```
SelectElementsByFilter(element_type: "BEAM", phase: 1)
CreateAssemblyDrawing()

For each drawing created:
    OpenDrawing(mark: "A1")
    AddPartMarks()
    AddBoltMarks()
    AddWeldMarks()
    AddOverallDimensions()
    PlaceControlDiagonals()
    ArrangeMarksNoCollisions()
    FitViewsToSheet()
    CloseActiveDrawing()
```

#### Phase 13: Export
```
ExportToPdf(mark: "A1")
SelectElementsByFilter(element_type: "BEAM")
ExportNC()
GenerateReport(reportTemplateName: "Steel_BOM.xsr", outputFileName: "BOM_Report.txt")
CheckNumberingStatus()
```

---

## 5. COMMON TASK RECIPES

### 5.1 Recipe: "Add a Column"
```
ModelBeam(profile: "HEB300", material: "S355JR",
    x1: X, y1: Y, z1: 0,
    x2: X, y2: Y, z2: height)
```

### 5.2 Recipe: "Add a Horizontal Beam"
```
ModelBeam(profile: "IPE400", material: "S355JR",
    x1: X1, y1: Y1, z1: Z,
    x2: X2, y2: Y2, z2: Z)
```

### 5.3 Recipe: "Connect Two Members" (System Macros)
```
AddConnection(primaryGuid: "col-guid", secondaryGuid: "beam-guid",
    connectionName: "EndPlate", connectionNumber: 144)
```

### 5.4 Recipe: "Apply a TSEP Plugin" (e.g., PEB Components)
Tekla plugins (like the PEB TSEP extensions from Tekla Warehouse) only use a `Name`. For the `connectionNumber`, you MUST pass the magic internal plugin identifier `-100000` (which corresponds to `BaseComponent.PLUGIN_OBJECT_NUMBER`).

**Available PEB Plugin Names (Exact Strings):**
- `"PEBMember"`
- `"PEBBasePlate"`
- `"PEBSpliceConnection"`
- `"PEBKneeJoint"`
- `"PEB EndPlate Flange Connection"`
- `"PEB EndPlate Flange Rotated"`
- `"PEB Steel Line"`
- `"PEB Anti Sag Roof and Wall"`
- `"PEB Anti Sag Apex"`
- `"PEB Member Updater"`
- `"PEB Ridge Rafter By Three Points"`
- `"PEB Ridge Rafter By Two Rafters"`

### 5.5 Applying Connection Attribute Presets (Templates)
Our `AddConnection` tool supports passing an `attributeFile` which loads the exact engineering variables (bolt sizes, welds, thicknesses) saved in the Tekla UI (e.g., standard `.xml` files). If not provided, it defaults to `"standard"`.

```
# Example: PEB Base Plate using a saved preset "heavy_duty_base"
AddConnection(primaryGuid: "plate-guid", secondaryGuid: "col-guid",
    connectionName: "PEBBasePlate", connectionNumber: -100000, attributeFile: "heavy_duty_base")

# Example: PEB Knee Connection using default "standard" preset
AddConnection(primaryGuid: "col-guid", secondaryGuid: "rafter-guid",
    connectionName: "PEBKneeJoint", connectionNumber: -100000, attributeFile: "standard")
```

### 5.6 Recipe: "Apply a User-Defined Custom Component"
For user-defined Custom Components, pass `-1` for the connection number (which corresponds to `BaseComponent.CUSTOM_OBJECT_NUMBER`).
```
AddConnection(primaryGuid: "col-guid", secondaryGuid: "beam-guid",
    connectionName: "My_Company_Haunch_V2", connectionNumber: -1, attributeFile: "standard")
```

### 5.6 Recipe: "Copy a Frame Along Y"
If you have all GUIDs of a portal frame at Y=0, copy them to Y=8000:
```
CopyObject(guid: "column-guid", dX: 0, dY: 8000, dZ: 0)
CopyObject(guid: "rafter-guid", dX: 0, dY: 8000, dZ: 0)
CopyObject(guid: "brace-guid", dX: 0, dY: 8000, dZ: 0)
```
This is the FASTEST way to build repetitive structures!

### 5.5 Recipe: "Build a Truss"
```
# Top chord
ModelBeam(profile: "L100X10", x1:0,y1:0,z1:12000, x2:10000,y2:0,z2:12000)

# Bottom chord
ModelBeam(profile: "L100X10", x1:0,y1:0,z1:11000, x2:10000,y2:0,z2:11000)

# Verticals (every 2000mm)
For i = 0 to 10000 step 2000:
    ModelBeam(profile: "L60X6", x1:i,y1:0,z1:11000, x2:i,y2:0,z2:12000)

# Diagonals
For i = 0 to 8000 step 2000:
    ModelBeam(profile: "L60X6", x1:i,y1:0,z1:11000, x2:i+2000,y2:0,z2:12000)
```

### 5.6 Recipe: "Annotate an Existing Drawing"
```
ListDrawings()                    → Find the drawing mark
OpenDrawing(mark: "A1")           → Open it
AddPartMarks()                    → Label parts
AddBoltMarks()                    → Label bolts
AddWeldMarks()                    → Label welds
AddOverallDimensions()            → Dimensions
AddSectionView(x1, y1, x2, y2)   → Add a section cut
AddDetailView(cx, cy, rx, ry)     → Add a detail bubble
ArrangeMarksNoCollisions()        → Clean up
FitViewsToSheet()                 → Optimize layout
CloseActiveDrawing(save: true)    → Save
```

### 5.7 Recipe: "Full QC Audit Before Issue"
```
CheckNumberingStatus()
GetDrawingStatusReport()
SelectElementsByFilter(element_type: "BEAM")
GenerateReport(reportTemplateName: "Steel_BOM.xsr", outputFileName: "audit.txt")
For each drawing:
    GetDrawingDetails(mark)
    GetDrawingObjectCount()
```

### 5.8 Recipe: "Issue Drawings for Fabrication"
```
For each drawing mark:
    UpdateDrawing(mark)
    SetDrawingLifecycle(mark, isLocked: true, isFrozen: true, isIssued: true)
    ExportToPdf(mark)
```

### 5.9 Recipe: "Import a Reference Model for Coordination"
```
InsertReferenceModel(filePath: "C:\\Models\\Architectural.ifc", x: 0, y: 0, z: 0)
AddBasePoint(name: "Site Origin", x: 0, y: 0, z: 0,
    latitude: 25.276987, longitude: 55.296249, altitude: 5.0)
```

### 5.10 Recipe: "Create a Concrete Foundation"
```
# Pad footing
ModelContourPlate(profile: "PL600", material: "C30",
    pointCoordinates: "-1500,-1500,-600;1500,-1500,-600;1500,1500,-600;-1500,1500,-600")

# Add rebar
AddRebarGroup(partGuid: "footing-guid", rebarSize: "16", rebarGrade: "A500HW",
    spacing: 200, x1: -1300,y1: -1300,z1: -550, x2: 1300,y2: 1300,z2: -50)

# Calculate pour
CalculatePourUnits()
```

---

## 6. DECISION TREES

### 6.1 "User wants to create a structure"
```
Does a grid exist?
├── NO → CreateGrid() first
└── YES
    ├── What type?
    │   ├── Steel → ModelBeam() for members
    │   ├── Concrete → ModelContourPlate() for slabs/walls
    │   └── Both → Mix of both
    ├── Are there connections?
    │   ├── System connection available → AddConnection()
    │   └── Custom → ModelContourPlate() + AddBolts() + AddWeld()
    └── Repetitive?
        ├── YES → Build one frame, then CopyObject() to replicate
        └── NO → Build each member individually
```

### 6.2 "User wants drawings"
```
What type of drawing?
├── Plan/Elevation → CreateGADrawing()
├── Assembly shop drawing → CreateAssemblyDrawing()
├── Individual part → CreateSinglePartDrawing()
└── Concrete → CreateCastUnitDrawing()

After creation:
├── Open → OpenDrawing()
├── Annotate → AddPartMarks(), AddBoltMarks(), etc.
├── Dimension → AddOverallDimensions()
├── View manipulate → AddSectionView(), AddDetailView()
├── Clean up → ArrangeMarksNoCollisions(), FitViewsToSheet()
└── Save → CloseActiveDrawing()
```

### 6.3 "User wants to modify existing structure"
```
Do you have the GUID?
├── YES → Use directly
└── NO → SelectElementsByFilter() to find it
    └── GetElementsProperties() to confirm

What modification?
├── Move → MoveObject()
├── Copy → CopyObject()
├── Split → SplitBeam()
├── Cut/Notch → ApplyPartCut() or AddCutPlane()
├── Trim end → AddFitting()
├── Combine → CombineBeams()
├── Change attributes → SetElementsUDAs()
└── Delete → (select + delete via macro) RunTeklaMacro()
```

### 6.4 "User wants to export"
```
What format?
├── PDF (drawings) → ExportToPdf()
├── DSTV/NC (CNC) → ExportNC()
├── MIS files → CreateMISExport()
├── Web viewer → SaveAsWebModel()
├── BOM report → GenerateReport()
└── IFC → RunTeklaMacro("ExportIFC") or use Tekla export
```

---

## 7. ERROR HANDLING & RECOVERY

### 7.1 Common Errors and Fixes

| Error | Cause | Fix |
|-------|-------|-----|
| "No active drawing" | Tried to annotate without opening | Call `OpenDrawing()` first |
| "Object not found from GUID" | GUID is stale or wrong | Use `SelectElementsByFilter()` to find correct GUID |
| "Failed to insert beam" | Invalid profile string | Check Tekla catalog — use exact profile names |
| "Parts not found" | GUIDs point to non-Part objects | Verify with `GetElementsProperties()` |
| "Move operation failed" | Object is locked or in frozen assembly | Check with `GetAllElementsUDAs()` |

### 7.2 Recovery Strategy
```
If a tool returns "❌ Failed...":
    1. DisplayPrompt("Checking issue...")
    2. Re-verify inputs (GUIDs, coordinates)
    3. Try GetElementsProperties() on the target
    4. Retry with corrected parameters
    5. If still failing: report to user with details
```

### 7.3 Drawing Lock Issues
```
If drawing operations fail:
    1. CloseActiveDrawing() — save what's open
    2. GetDrawingStatusReport() — check if locked/frozen
    3. SetDrawingLifecycle(mark, isLocked: false, isFrozen: false, isIssued: false)
    4. Retry the operation
```

---

## 8. BEST PRACTICES

### 8.1 Efficiency
- **Use CopyObject for repetitive structures.** Build one portal frame, then copy it N times along an axis. This is 10× faster than building each member individually.
- **Batch your selections.** Use `SelectElementsByFilter()` with specific criteria rather than selecting one element at a time.
- **Use phases.** Always assign parts to construction phases so drawings and reports can be filtered.

### 8.2 Naming Conventions
- Columns: Name = "COLUMN", Class = "1"
- Beams: Name = "BEAM", Class = "2"
- Bracing: Name = "BRACE", Class = "3"
- Plates: Name = "PLATE", Class = "4"
- Purlins: Name = "PURLIN", Class = "5"

### 8.3 Common Profile Strings (Tekla Format)
| Type | Example | Description |
|------|---------|-------------|
| H-sections | `HEA300`, `HEB400`, `HEM500` | European H-beams |
| I-sections | `IPE300`, `IPE400`, `IPE500` | European I-beams |
| W-shapes | `W12X26`, `W14X30` | American wide flanges |
| Channels | `UPN200`, `C250X45` | U-channels |
| Angles | `L100X100X10`, `L80X80X8` | Equal angles |
| Tubes | `RHS200X100X8`, `SHS150X8` | Rectangular/square tubes |
| Pipes | `CHS168.3X8` | Circular hollow sections |
| Plates | `PL10`, `PL20`, `PL25` | Flat plates (thickness) |
| Z-purlins | `200Z20`, `250Z25` | Cold-formed Z-sections |

### 8.4 Common Material Strings
| Material | Use |
|----------|-----|
| `S235JR` | General structural steel (mild) |
| `S275JR` | Medium strength steel |
| `S355JR` | High strength structural steel |
| `S350GD` | Cold-formed steel (purlins) |
| `A500HW` | Reinforcing bar steel |
| `C30` | Concrete 30 MPa |
| `C40` | Concrete 40 MPa |

### 8.5 Common Connection Numbers (System Components)
| Number | Name | Description |
|--------|------|-------------|
| 1 | Base plate | Column base connection |
| 11 | Shear plate | Simple shear connection |
| 17 | End plate | Moment connection |
| 27 | Clip angle | Beam-to-column clip |
| 63 | Stiffener | Web stiffener plates |
| 77 | Splice | Beam splice connection |
| 141 | Haunch | Haunched connection |
| 142 | Haunch (alt) | Alternative haunch macro |
| 143 | Gusset | Brace gusset plate |
| 144 | End plate | Full depth end plate |
| 146 | Base plate | Extended base plate |

### 8.6 Drawing Annotation Order
Always annotate in this order for best results:
1. `AddPartMarks()` — Parts first (they take priority in layout)
2. `AddBoltMarks()` — Bolts second
3. `AddWeldMarks()` — Welds third
4. `AddOverallDimensions()` — Dimensions after marks
5. `PlaceControlDiagonals()` — Check dimensions
6. `ArrangeMarksNoCollisions()` — **LAST** — clean up all overlaps
7. `FitViewsToSheet()` — Final layout optimization

### 8.7 Coordinate Quick Reference
```
For a building at origin:
- Column at Grid A-1: x=0, y=0
- Column at Grid B-1: x=spacing, y=0
- Eave height: z=eave_height
- Foundation: z = -depth (negative = below ground)

For copying frames along Y:
- dX=0, dY=frame_spacing, dZ=0

For copying bays along X:
- dX=bay_spacing, dY=0, dZ=0
```

---

## 9. TOOL DEPENDENCY MAP

```
CreateGrid ──→ ModelBeam ──→ AddConnection
                  │              │
                  ├──→ AddWeld   ├──→ AddBolts
                  │              │
                  ├──→ CopyObject (replicate frames)
                  │
                  ├──→ ModelContourPlate (plates)
                  │         │
                  │         └──→ AddBolts / AddWeld
                  │
                  └──→ ManagePhases
                           │
                           └──→ SelectElementsByFilter
                                    │
                                    ├──→ CreateAssemblyDrawing
                                    │         │
                                    │         └──→ OpenDrawing
                                    │                   │
                                    │                   ├──→ AddPartMarks
                                    │                   ├──→ AddBoltMarks
                                    │                   ├──→ AddWeldMarks
                                    │                   ├──→ AddOverallDimensions
                                    │                   ├──→ ArrangeMarksNoCollisions
                                    │                   ├──→ FitViewsToSheet
                                    │                   └──→ CloseActiveDrawing
                                    │                             │
                                    │                             └──→ ExportToPdf
                                    │
                                    ├──→ ExportNC
                                    ├──→ GenerateReport
                                    └──→ CheckNumberingStatus
```

---

## 10. FUTURE SCALABILITY & AI MEMORY

To ensure smooth scalability when building "N numbers of steel projects," this workspace contains two critical directories. **AI Agents MUST utilize these directories** to avoid repeating mistakes and guessing parameters.

### 10.1 `PROJECT_TEMPLATES/`
Do not guess how to build a company's specific PEB building or tower. The user stores parameterized data schemas here (e.g., `PEB_100x100.md`, `Tower_V2.json`).
* **Before modeling**: AI must ask the user: *"Which template should I use from the PROJECT_TEMPLATES folder?"*
* **What's inside**: Exact grid spacings, strict profile strings (`Z200*2.0`), material grades, and TSEP Plugin names/IDs to use.

### 10.2 `AGENT_MEMORY/`
If you (the AI) encounter an error trying to insert a part, call a macro, or configure a drawing, and you manage to fix it: **YOU MUST DOCMENT IT**.
* **File**: `AGENT_MEMORY/ERROR_LOGS.md`
* **Workflow**: Whenever you start a new session, briefly read this file. If you hit a roadblock during execution, check this file to see if a past agent solved the same issue. If you solve a new issue, write the scenario and the resolution rule into the file. This ensures the entire system gets smarter over time.

---

## 11. RESEARCHING THE API: THE SEMANTIC SERVICE

When a user asks for a feature that isn't explicitly listed in this playbook or the toolset, you must act as a **Detective** using the local semantic service.

### 11.1 The `api-members.txt` Master Map
The file `semantic-service\api-members.txt` contains a dump of all ~14,000 Tekla Open API members. Use it to find exact property names and method signatures.

**Strategy for finding new API calls:**
1.  **Keyword Search**: Use `grep` or `sls` to search for keywords (e.g., "Visibility", "Hide", "Identifier").
2.  **Bridging 2D and 3D**: Drawing objects (in `Tekla.Structures.Drawing`) often link to model objects via the `ModelIdentifier` property. 
3.  **Property Retrieval**: Use `GetReportProperty("NAME", ref value)` for most model data.
4.  **Action Methods**: Look for methods like `Modify()`, `Delete()`, or `Insert()` to commit changes.

### 11.2 Example Case: Hiding by Phase
*   **Discovery**: Searching for "Hide" revealed `Hideable.HideFromDrawing()`.
*   **Mapping**: Searching for "Identifier" revealed `ModelObject.ModelIdentifier` for 2D objects.
*   **Result**: The logic for `SelectPhaseInDrawing` was built by selecting a model object from the drawing's identifier, checking its phase, and calling the `Hideable` API on the 2D object.

---

> **Remember**: The AI agent should ALWAYS think in terms of "Grid first → Members → Connections → Phases → Drawings → Export". This is the natural structural engineering workflow, and it maps directly to the tool dependency chain.
