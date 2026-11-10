# 🏗️ Project Templates Library

> **Purpose**: Instead of asking the AI to guess the parameters of a building every time, store JSON or Markdown templates here. The AI can read these files to get exact profiles, grid spacings, and connection types before it starts calling MCP tools.

## How to use this folder
When you ask the AI to build something, say:
*"Read the PEB template from PROJECT_TEMPLATES/PEB_100x100.md and build it."*

---

### Example Template Structure: `PEB_Standard.md`

```markdown
# Building Parameters
- **Type**: Pre-Engineered Building (PEB)
- **Grid X**: 0, 10*8000 (10 bays of 8m)
- **Grid Y**: 0, 20000 (1 span of 20m)
- **Grid Z**: 0, 7500 (Eave height)

# Section Profiles
- **Main Columns**: Built-up: `PL10*300+PL8*500` (Material: `S355JR`)
- **Main Rafters**: Built-up: `PL10*250+PL8*450` (Material: `S355JR`)
- **Roof Purlins**: Cold-formed: `Z200*2.0` (Material: `S350GD`)
- **Wall Girts**: Cold-formed: `Z200*2.0` (Material: `S350GD`)
- **Bracing**: `L100X10` (Material: `S235JR`)

# Connection Mapping (Include optional attribute presets!)
- **Column to Foundation**: TSEP Plugin `"PEBBasePlate"` (Number: `-100000`, Preset: `"heavy_duty_base"`)
- **Column to Rafter**: TSEP Plugin `"PEBKneeJoint"` (Number: `-100000`, Preset: `"standard"`)
- **Apex Splice**: TSEP Plugin `"PEBRidgeRafterPlugin"` (Number: `-100000`, Preset: `"standard"`)
```

> *(Add your company's standard building templates in this directory)*

---

## Complete Blueprint Example: PEB 100m × 100m × 12m
If an AI agent is asked to build a 100m x 100m x 12m PEB, this is exactly what the internal execution logic looks like based on the template logic.

### 1. Structural Grid
```
CreateGrid(
    xOffsets: "0.0 12*8333.0",
    yOffsets: "0.0 12*8333.0",
    zOffsets: "0.0 12000.0",
    xLabels: "A B C D E F G H I J K L M",
    yLabels: "1 2 3 4 5 6 7 8 9 10 11 12 13",
    zLabels: "GL EAVE"
)
```

### 2. Perimeter Columns (52 total)
*(13 on Front/Back, 13 on Left/Right, minus corners)*
```
ModelBeam(
    profile: "HEA400",
    material: "S355JR",
    x1: gridX, y1: gridY, z1: 0,
    x2: gridX, y2: gridY, z2: 12000
)
```

### 3. Eave Rafters
```
ModelBeam(
    profile: "IPE500",
    material: "S355JR",
    x1: 0, y1: frameY, z1: 12000,
    x2: 100000, y2: frameY, z2: 12000
)
```

### 4. PEB Connections (Using TSEP Plugins + Attribute Templates)
```
AddConnection(
    primaryGuid: columnGuid,
    secondaryGuid: rafterGuid,
    connectionName: "PEBKneeJoint",
    connectionNumber: -100000,
    attributeFile: "standard"
)

AddConnection(
    primaryGuid: basePlateGuid,
    secondaryGuid: columnGuid,
    connectionName: "PEBBasePlate",
    connectionNumber: -100000,
    attributeFile: "heavy_duty_base"
)
```

### 5. Drawing Generation
```
SelectElementsByFilter(element_type: "BEAM", phase: 1)
CreateAssemblyDrawing()
For each drawing created:
    OpenDrawing(mark: "A1")
    AddPartMarks()
    AddOverallDimensions()
    ArrangeMarksNoCollisions()
    FitViewsToSheet()
    CloseActiveDrawing()
```
