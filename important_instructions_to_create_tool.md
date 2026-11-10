# Important Instructions for Creating Tekla MCP Tools

> [!CAUTION]
> **DO NOT MODIFY THIS FILE.** These instructions are verified and finalized.
> If new rules are needed, ADD them at the bottom under a new dated section.
> Never remove or change existing entries — they are battle-tested fixes.

This document provides **definitive, verified** guidelines for developing tools within the **TeklaMcpServer**. Every rule here was learned from actual build failures and validated against the **official TSOpenAPIExamples-2022** reference project.

---

## 1. Environment & Architecture

### Working Directory (CRITICAL)
The server **must** set its working directory to the Tekla bin folder **before anything else**:
```csharp
System.IO.Directory.SetCurrentDirectory(@"C:\TeklaStructures\2022.0\bin");
```
Without this, all Tekla DLL loading will fail silently at runtime.

### Project Specs
| Setting | Value |
|---------|-------|
| Target Framework | `net48` (.NET Framework 4.8) |
| Output Type | `Exe` (Console — required for Stdio transport) |
| C# Language | `latest` |
| SDK used to build | .NET 8 SDK (builds net48 target) |

### NuGet Dependencies
```xml
<PackageReference Include="ModelContextProtocol" Version="1.2.0" />
<PackageReference Include="Microsoft.Bcl.AsyncInterfaces" Version="10.0.5" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
```

### Tekla DLL References (local, not NuGet)
```xml
<Reference Include="Tekla.Structures">
  <HintPath>C:\TeklaStructures\2022.0\bin\Tekla.Structures.dll</HintPath>
</Reference>
<Reference Include="Tekla.Structures.Model">
  <HintPath>C:\TeklaStructures\2022.0\bin\Tekla.Structures.Model.dll</HintPath>
</Reference>
<Reference Include="Tekla.Structures.Drawing">
  <HintPath>C:\TeklaStructures\2022.0\bin\Tekla.Structures.Drawing.dll</HintPath>
</Reference>
```

---

## 2. Namespace Ambiguity Resolution (CRITICAL)

Both `Tekla.Structures.Model` and `Tekla.Structures.Drawing` define types with the same name (`ModelObject`, `Part`, etc.). You **MUST** use namespace aliases to avoid ambiguity:

```csharp
using TSM = Tekla.Structures.Model;
using TSD = Tekla.Structures.Drawing;
using ModelObjectSelector = Tekla.Structures.Model.UI.ModelObjectSelector;
```

Then always qualify:
- `TSM.ModelObject` — model-side objects
- `TSM.Part` — model-side parts (has `GetSolid()`)
- `TSD.Drawing` — drawing objects
- `TSD.Part` — drawing-side part representations (has `ModelIdentifier`)

> [!WARNING]
> Using bare `ModelObject` or `Part` will cause CS0104 ambiguity errors.
> **Never** write `using Tekla.Structures.Model;` AND `using Tekla.Structures.Drawing;` without aliases.

---

## 3. Tekla 2022 API — Verified Correct Patterns

### A. Drawing Identifiers
- ❌ `drawing.Identifier` — does NOT exist as a public property
- ❌ `drawing.DrawingIdentifier` — does NOT exist
- ✅ `drawing.GetIdentifier()` — extension method from `Tekla.Structures.DrawingInternal`
- ✅ `drawing.Mark` — use this as the primary drawing identifier (string)

### B. Getting Model Object from Drawing Part
- ❌ `drawingHandler.GetModelObjectFromDrawingObject(part)` — this method does NOT exist
- ✅ Pattern from **DimensionExample.cs**:
```csharp
Identifier modelId = drawingPart.ModelIdentifier;
TSM.ModelObject modelObj = new TSM.Model().SelectModelObject(modelId);
TSM.Part modelPart = modelObj as TSM.Part;
```

### C. Bounding Box / Solid Geometry
- ❌ `modelObject.GetAxisAlignedBoundingBox()` — does NOT exist on ModelObject
- ✅ Pattern from **InsertMarkPlugin.cs** and **ReinforcedBeam.cs**:
```csharp
TSM.Part modelPart = (TSM.Part)modelObj;
modelPart.Select();
Solid solid = modelPart.GetSolid();
Point pMin = solid.MinimumPoint;
Point pMax = solid.MaximumPoint;
```

### D. Creating Dimensions
- ❌ `new StraightDimension(view, point1, point2, vector, distance, attrs)` — constructor does NOT exist
- ❌ `new StraightDimension(view, pointList, point, attrs)` — constructor does NOT exist
- ❌ `StraightDimensionAttributes` as standalone class — does NOT exist
- ✅ Pattern from **DimensionExample.cs**:
```csharp
PointList pointList = new PointList();
pointList.Add(startPoint);
pointList.Add(endPoint);

var dimHandler = new StraightDimensionSetHandler();
StraightDimensionSet dims = dimHandler.CreateDimensionSet(
    viewBase,       // ViewBase (not View)
    pointList,      // PointList
    new Vector(0, -1, 0),  // UpDirection
    100             // Distance in mm
);
```

> [!IMPORTANT]
> Always set the **TransformationPlane** to the view's coordinate system before getting solid points,
> and restore it afterwards:
> ```csharp
> TransformationPlane savePlane = myModel.GetWorkPlaneHandler().GetCurrentTransformationPlane();
> myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(
>     new TransformationPlane(view.DisplayCoordinateSystem));
> // ... do work ...
> myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(savePlane);
> ```

### E. PDF Export (DPMPrinterAttributes)
- ❌ `ScalingMethodType` — does NOT exist
- ❌ `PrintToFileAttributes.OutputPath` / `PrintToFileAttributes.FileName` — wrong property names
- ✅ **Verified pattern from Print_Drawing_DPM/Logic.cs:**
```csharp
DPMPrinterAttributes printAttributes = new DPMPrinterAttributes();
printAttributes.ColorMode = DotPrintColor.BlackAndWhite;
printAttributes.NumberOfCopies = 1;
printAttributes.Orientation = DotPrintOrientationType.Landscape;
printAttributes.OutputType = DotPrintOutputType.PDF;
printAttributes.PaperSize = DotPrintPaperSize.A4;
printAttributes.ScalingMethod = DotPrintScalingType.Auto;
printAttributes.ScaleFactor = 1.0;
printAttributes.PrintToMultipleSheet = DotPrintToMultipleSheet.Off;
printAttributes.OutputFileName = "output.pdf";

bool success = drawingHandler.PrintDrawing(drawing, printAttributes);
// PrintDrawing takes exactly 2 args: (Drawing, DPMPrinterAttributes)
```

### F. Part Marks
- ❌ `PartMark` class — does NOT exist in Tekla 2022
- ❌ `mark.Attributes.LoadAttributes("standard")` — wrong method name
- ✅ **Verified pattern from InsertMarkPlugin.cs:**
```csharp
Mark mark = new Mark(drawingPart);  // takes a drawing ModelObject/Part
mark.Attributes.Content.Clear();
mark.Attributes.Content.Add(new UserDefinedElement("PROFILE"));
mark.InsertionPoint = new Point(0, 0, 0);
mark.Insert();
```

### G. Arranging Objects / Auto-Layout
- ❌ `DrawingOperation.ArrangeObjects(view)` — does NOT exist
- ❌ `Operation.ArrangeObjects(sheet, settings)` — does NOT exist
- ❌ `ArrangeObjectSettings` class — does NOT exist
- ✅ `drawing.PlaceViews()` — the correct 2022 API for auto-arranging views on a sheet

### H. Drawing Enumeration
✅ **Correct pattern:**
```csharp
DrawingHandler handler = new DrawingHandler();
DrawingEnumerator drawings = handler.GetDrawings();
while (drawings.MoveNext())
{
    if (drawings.Current is TSD.Drawing d)
    {
        // Use d.Mark, d.Name, etc.
    }
}
```

### I. Boolean Part Operations
```csharp
var cp = new Tekla.Structures.Model.BooleanPart();
cp.Father = mainPart;
cp.OperativePart = cuttingPart;  // NOT SetSecondaryObject
cp.Insert();
```

---

## 4. Tool Registration Rules

Each tool must be:
1. A `public static` method inside a class decorated with `[McpServerToolType]`
2. Decorated with `[McpServerTool]` and `[Description("...")]`
3. Return type: `string` (MCP response)
4. Parameters: simple types (`string`, `int`, `string[]`, `Dictionary<string, string>`)

```csharp
[McpServerToolType]
public static class TeklaTools
{
    [McpServerTool, Description("Human-readable description")]
    public static string MyNewTool(string param1, int param2 = 0)
    {
        try
        {
            // tool logic
            return "✅ Success message";
        }
        catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
    }
}
```

> [!TIP]
> Every tool should be wrapped in try/catch and return user-friendly error messages.

---

## 5. Build & Deployment

### Building
```powershell
cd C:\Users\dhilip.kumar\TeklaMcpServer
dotnet build
```

### Fixing File Locks
If build fails with `MSB3021: Unable to copy file... used by another process`:
```powershell
taskkill /F /IM TeklaMcpServer.exe
dotnet build
```

### Deployment Checklist
- [ ] Tekla Structures 2022 is running with a model open
- [ ] `TeklaMcpServer.exe` is built in `bin/Debug/net48/`
- [ ] MCP config points to absolute path of the `.exe`
- [ ] **Restart the MCP client** (Antigravity/Claude) after any code change

---

## 6. Reference Projects

| Resource | Path |
|----------|------|
| **Official Tekla 2022 Examples** | `D:\Dhilip Kumar M\Coading\Tekla MCP\Others projects\TSOpenAPIExamples-2022` |
| **Print DPM Example** | `...\Drawings\Applications\Print_Drawing_DPM\Logic.cs` |
| **Dimension Example** | `...\Drawings\Applications\DimensionExample\DimensionExample.cs` |
| **Mark Plugin Example** | `...\Drawings\Plugins\InsertMarkPlugin\InsertMarkPlugin.cs` |
| **Drawing List Example** | `...\Drawings\Applications\SimpleDrawingList\SimpleDrawingList.cs` |
| **Original Python Server** | `...\TeklaMCPServer-main\mcp_server.py` |
| **Tekla Drawing XML Docs** | `C:\TeklaStructures\2022.0\bin\Tekla.Structures.Drawing.xml` |
| **Tekla Model XML Docs** | `C:\TeklaStructures\2022.0\bin\Tekla.Structures.Model.xml` |

> [!CAUTION]
> Always verify API usage against these reference examples BEFORE writing code.
> The Tekla 2022 API has many undocumented differences from newer versions.
> Do NOT guess constructor signatures — look them up in the examples or XML docs.

---

## 7. Files That Must Stay Empty

| File | Reason |
|------|--------|
| `TestIdentifier.cs` | Was a test probe — caused build errors with non-existent Drawing properties |
| `ProbeAttributes.cs` | Had conflicting `Main()` entry point |

> [!WARNING]
---

## 8. April 7, 2026 — Advanced Drawing findings

### A. View Creation (Section & Detail)
- ✅ `TSD.View.CreateSectionView(View, Point1, Point2, Point3, double, double, ViewAttributes, SectionMarkAttributes, out View, out SectionMark)`
- ✅ `TSD.View.CreateDetailView(View, CenterPoint, RadiusPoint, LabelPoint, InsertionPoint, ViewAttributes, DetailMarkAttributes, out View, out DetailMark)`
- *Tip*: Points must be in the parent view's coordinate system.

### B. DwgObject Constructor (CRITICAL)
- ❌ `new TSD.DwgObject(targetView, filePath, point)`
- ✅ `new TSD.DwgObject(targetView, point, filePath)`
- The constructor requires the `Point` argument **before** the string path.

### C. Drawing Lifecycle (IsIssued)
- ❌ `drawing.IsIssued = true;` — **Read-only property error CS0200.**
- ✅ `handler.IssueDrawing(drawing);`
- ✅ `handler.UnissueDrawing(drawing);`

### D. Multi-Macro Execution
- ✅ `Tekla.Structures.Model.Operations.Operation.RunMacro(string macroName)`
- Works in **both** Model and Drawing modes. If a drawing is active, it runs drawing macros; if the model is active, it runs model macros.

### E. Cast Unit Drawings
- ✅ `new TSD.CastUnitDrawing(part.Identifier).Insert();`
- Verified constructor for concrete cast units in Tekla 2022.

---

## 9. April 7, 2026 — 3D Model & Generative findings

### A. Grids
- ✅ `TSM.Grid` uses string offsets: `CoordinateX = "0.0 4*6000.0"`
- ✅ Labels must match coordinate count: `LabelX = "1 2 3 4 5"`

### B. Contour Plates (Slabs/Floors)
- ✅ `TSM.ContourPlate` requires a `Contour` populated with `ContourPoint` objects.
- ✅ `ContourPoint` constructor: `new ContourPoint(new Point(x, y, z), Chamfer)` (use `null` for no chamfer).

### C. System Components (Connections)
- ✅ `TSM.Connection` represents Tekla Macros.
- ✅ Mandatory fields: `Name`, `Number` (the macro ID, e.g., 144), `SetPrimaryObject()`, `SetSecondaryObject()`.

### D. 3D Detailing (Welds & Bolts)
- ✅ `TSM.Weld`: Use `MainObject` and `SecondaryObject` (both `TSM.Part`).
- ✅ `TSM.BoltArray`: Use `PartToBeBolted` and `PartToBoltTo`. Requires `FirstPosition` and `SecondPosition` to define the bolt group's direction.

### E. BIM Operations
- ✅ `Operation.CreateReportFromSelected(template, file, title1, title2, title3)`: Title arguments cannot be null (use `""`).
- ✅ `Operation.CreateNCFilesFromSelected(ncSettingsName, outputFolder)`: Typically uses `"DSTV for profiles"` or `"DSTV for plates"`.

---

## 10. April 8, 2026 — Tier 11-22 Expansion (19 new tools)

### A. Spatial Operations
- ✅ `Operation.MoveObject(ModelObject, Vector)` — returns `bool`.
- ✅ `Operation.CopyObject(ModelObject, Vector)` — returns `ModelObject` (the new copy).
- ✅ `Operation.Split(Beam, Point)` — void return, auto-creates two new beams.

### B. Generative Steel
- ✅ `TSM.Beam` requires `StartPoint`, `EndPoint`, `Profile.ProfileString`, `Material.MaterialString`.
- ✅ Always set `Position.Depth`, `Position.Plane`, `Position.Rotation` for predictable placement.
- ✅ `TSM.PolyBeam` uses `AddContourPoint()` same as `ContourPlate`.
- ✅ `TSM.BoltCircle` uses `NumberOfBolts` and `Diameter` properties (not available in `BoltArray`).

### C. Drawing Graphics
- ✅ `TSD.Line(ViewBase, Point, Point)` — constructor for drawing lines.
- ✅ `TSD.Circle(ViewBase, Point, double)` — center point + radius.
- ✅ `TSD.Cloud(ViewBase, PointList)` — revision clouds from a polygon of points.
- ✅ `TSD.Rectangle(ViewBase, Point, Point)` — two corner points.

### D. Numbering QA
- ✅ `Operation.IsNumberingUpToDateAll()` returns `bool`.
- ✅ `Operation.GetSimilarNumberedObjects(ModelObject)` returns `List<ModelObject>` (NOT an enumerator — use `foreach`, not `MoveNext`).

### E. Reference Models & Surface
- ✅ `TSM.ReferenceModel` uses `Filename` and `Position` properties.
- ✅ `TSM.SurfaceTreatment` uses `Thickness` property (NOT `SurfaceArea` — that doesn't exist).

### F. Cuts & Fittings
- ✅ `TSM.CutPlane` and `TSM.Fitting` both need `Father = part` and `Plane = new TSM.Plane()`.
- ✅ Set `Plane.Origin` and `Plane.AxisX` to define the cutting geometry.

---

## 11. April 8, 2026 — Final Batch Findings (Tiers 15-19)

### A. ControlLine
- ❌ `Point1` / `Point2` DO NOT EXIST. Use `Line` property (type `LineSegment`).
- ✅ Correct: `cl.Line = new LineSegment(p1, p2);`

### B. ControlPlane
- ❌ `Extension` property DOES NOT EXIST in Tekla 2022.
- ✅ Only set `Plane.Origin` and `Plane.AxisX`.

### C. BasePoint
- ❌ Parameterless constructor DOES NOT EXIST.
- ✅ Use `new BasePoint(string name)` constructor.
- ❌ `Location` and `Altitude` properties DO NOT EXIST.
- ✅ Use `LocationInModelX`, `LocationInModelY`, `LocationInModelZ`, `Elevation`.

### D. GetSimilarNumberedObjects
- ❌ Returns `List<ModelObject>`, NOT `ModelObjectEnumerator`.
- ✅ Use `foreach`, NOT `MoveNext()/Current`.

### E. SurfaceTreatment
- ❌ `SurfaceArea` DOES NOT EXIST.
- ✅ Use `Thickness` property.

---

## 12. November 4, 2026 — Baseplate Holes, Anchor Bolt Clearance & Tekla C# 5 Macro Execution

### A. Inspecting Holes & Bolts on Model Parts (CRITICAL)
- ❌ Do NOT rely solely on part report properties like `NUMBER_OF_HOLES` — they do not provide bolt diameters, tolerances, or slot geometry.
- ✅ Use `part.GetBolts()` which returns a `ModelObjectEnumerator` of `TSM.BoltGroup` objects (`BoltArray`, `BoltCircle`, `BoltXYList`).
- `bg.Bolt`: When `false`, it represents pure holes (no bolt hardware modeled, e.g. anchor bolt holes in base plates). When `true`, actual bolts with holes are modeled.
- `bg.BoltSize`: Base nominal diameter (e.g. `20` for M20, `24` for M24).
- `bg.Tolerance`: Hole clearance (e.g. `6` mm for anchor bolt clearance).
- Hole Diameter: `bg.BoltSize + bg.Tolerance` (or report property `HOLE.DIAMETER` / `DIAMETER`).
- Slotted Holes: `bg.HoleType == BoltGroup.BoltHoleTypeEnum.HOLE_TYPE_SLOTTED`. Check `bg.SlottedHoleX` and `bg.SlottedHoleY`.
- Hole Count: `bg.BoltPositions.Count`.
- GUID tracking: Always track `bg.Identifier.GUID` in a `HashSet<string>` to avoid double-counting when multiple connected parts share the same bolt group.

### B. Tekla Macro Runtime Constraints (C# 5 Language Level)
Tekla Structures' internal macro compiler targets C# 5:
- ❌ String Interpolation `$"..."`: **Causes `error CS1056: Unexpected character '$'`**.
- ✅ Always use `string.Format(...)` or string concatenation (`+`) in `.cs` macros.
- ❌ C# 7+ Pattern matching `if (obj is Part p)`:
- ✅ Always use standard cast: `Part p = obj as Part; if (p != null) { ... }`.
- ❌ Ambiguous `ModelObjectSelector`:
- ✅ Fully qualify: `Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();` (because `Tekla.Structures.Model.ModelObjectSelector` also exists and has no public constructor).
- ❌ `BooleanItem`:
- ✅ Use `Tekla.Structures.Model.BooleanPart`.

### C. New MCP Tool: GetHolesSummary
- Implemented `GetHolesSummary(string guids = "")` in `TeklaMcpServer.Tools.QueryTools`.
- Inspects active UI selection or semicolon-separated GUIDs.
- Returns comprehensive breakdown: Hole Dia (e.g. M20 -> 26mm Dia, 774 NOS), Hole Type, Bolt vs Hole Only, and count per profile.

### D. New MCP Tool: GetBasePlateTemplates
- Implemented `GetBasePlateTemplates(string guids = "", string prefix = "T", bool groupByProfile = false)` in `TeklaMcpServer.Tools.QueryTools`.
- Inspects selected base plates and groups identical anchor bolt patterns (Pitch x Gauge, Bolt Size, Hole Diameter, and Hole Count).
- Produces site erection and fabrication template schedules (e.g. `T1 = 129 NOS`, `T2 = 4 NOS`).
- Uses `BoltArray.GetBoltDistXCount()`, `BoltArray.GetBoltDistX(i)`, `BoltArray.GetBoltDistYCount()`, `BoltArray.GetBoltDistY(i)` to extract pitch and gauge spacings.

---
