# Tekla Open API Quick Reference (Tekla 2022)

## Model Creation

| Need | API |
|---|---|
| Create beam/column | `new Beam() { StartPoint, EndPoint, Profile.ProfileString, Material.MaterialString }.Insert()` |
| Create plate | `new ContourPlate() { Profile.ProfileString, Material.MaterialString }.AddContourPoint(Point).Insert()` |
| Create poly beam | `new PolyBeam() { Profile.ProfileString, Material.MaterialString }.AddContourPoint(Point).Insert()` |
| Create grid | `new Grid() { CoordinateX, CoordinateY, CoordinateZ, LabelX, LabelY, LabelZ }.Insert()` |
| Commit changes | `model.CommitChanges()` — **Required after every Insert/Modify/Delete** |

## Part Modification

| Need | API |
|---|---|
| Change profile | `part.Profile.ProfileString = "HEB400"; part.Modify();` |
| Change material | `part.Material.MaterialString = "S355JR"; part.Modify();` |
| Move part | `TSM.Operations.Operation.Move(offset, obj)` |
| Copy part | `TSM.Operations.Operation.Copy(offset, obj)` |
| Delete part | `part.Delete()` |
| Get part GUID | `part.Identifier.GUID.ToString()` |

## Connections & Bolts

| Need | API |
|---|---|
| Create bolt group | `new BoltArray() { PartToBoltTo, PartToBeBolted, BoltSize, BoltStandard }.Insert()` |
| Create bolt circle | `new BoltCircle() { NumberOfBolts, Diameter }.Insert()` |
| Create weld | `new Weld() { MainObject, SecondaryObject, SizeAbove }.Insert()` |
| Add fitting | `new Fitting() { Father = part, Plane = fittingPlane }.Insert()` |
| Add cut plane | `new CutPlane() { Father = part, Plane = cuttingPlane }.Insert()` |
| Boolean cut | `new BooleanPart() { Father, OperativePart, Type = BOOLEAN_CUT }.Insert()` |
| System component | `new Connection() { Name, Number, SetPrimaryObject(part), SetSecondaryObject(part) }.Insert()` |

## Selection & Enumeration

| Need | API |
|---|---|
| Selected objects | `new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects()` |
| All objects by type | `model.GetModelObjectSelector().GetAllObjectsWithType(ModelObject.ModelObjectEnum.BEAM)` |
| Select by GUID | `model.SelectModelObject(new Identifier(new Guid(guidString)))` |
| Set selection | `new Tekla.Structures.Model.UI.ModelObjectSelector().Select(arrayList)` |

## Catalogs

| Need | API |
|---|---|
| Profile catalog | `new CatalogHandler().GetLibraryProfileItems()` → `LibraryProfileItem.ProfileName` |
| Parametric profiles | `new CatalogHandler().GetParametricProfileItems()` → `ParametricProfileItem.ProfilePrefix` |
| Material catalog | `new CatalogHandler().GetMaterialItems()` → `MaterialItem.MaterialName` |
| Bolt catalog | `new CatalogHandler().GetBoltItems()` → `BoltItem.Standard, .Size` |
| Component catalog | `new CatalogHandler().GetComponentItems()` → `ComponentItem.Name, .Number` |

## Drawing API

| Need | API |
|---|---|
| Get drawing handler | `new DrawingHandler()` |
| List all drawings | `handler.GetDrawings()` → iterate |
| Open drawing | `handler.SetActiveDrawing(drawing, true)` |
| Close drawing | `handler.CloseActiveDrawing(true)` |
| Get active drawing | `handler.GetActiveDrawing()` |
| Create assembly drawing | `new AssemblyDrawing(assembly, "standard").Insert()` |
| Create GA drawing | `new GADrawing("name", "standard").Insert()` |
| Get views | `drawing.GetSheet().GetAllObjects(typeof(View))` |
| Commit drawing | `drawing.CommitChanges()` |

## Drawing Annotations

| Need | API |
|---|---|
| Add part mark | `new Mark(drawingPart) { InsertionPoint, Placing }.Insert()` |
| Add dimension | `new StraightDimensionSet(view, pointList, vectorUp, distFromPart).Insert()` |
| Add text | `new Text(view, insertionPoint, "text").Insert()` |
| Add line | `new Line(view, startPoint, endPoint).Insert()` |
| Add circle | `new Circle(view, centerPoint, radius).Insert()` |
| Add detail view | `new DetailView(parentView, centerPoint, radiusPoint, AxisDirection).Insert()` |
| Add section view | `new SectionView(parentView, point1, point2, depthUp, depthDown).Insert()` |

## Geometry / Coordinate Transforms

| Need | API |
|---|---|
| Part coordinate system | `part.GetCoordinateSystem()` → `CoordinateSystem (Origin, AxisX, AxisY)` |
| Set work plane | `model.GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane())` |
| Point distance | `Distance.PointToPoint(p1, p2)` |
| Global work plane | `new TransformationPlane()` — no args = global |

## Macros & Operations

| Need | API |
|---|---|
| Run macro | `Operation.RunMacro("macroName.cs")` — searches XS_MACRO_DIRECTORY |
| Check if running | `Operation.IsMacroRunning()` |
| Display prompt | `Operation.DisplayPrompt("message")` |
| Get advanced option | `TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref string)` |

## Reports & Export

| Need | API |
|---|---|
| Generate report | `Operation.CreateReportFromSelected("template.xsr", "output.txt", ...)` |
| Display report | `Operation.DisplayReport("filePath")` |
| Export NC | `Operation.RunMacro("ExportNC.cs")` |

## Phases & Assembly

| Need | API |
|---|---|
| Get phases | `model.GetPhases()` |
| Set part phase | `part.SetPhase(new Phase(number, name))` |
| Get assembly | `part.GetAssembly()` |

## Common Patterns

**Fresh connection per call:**
```csharp
var model = new TSM.Model();
if (!model.GetConnectionStatus()) return "❌ Not connected.";
```

**Work plane management (for drawings):**
```csharp
var savePlane = model.GetWorkPlaneHandler().GetCurrentTransformationPlane();
model.GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane(view.DisplayCoordinateSystem));
// ... work in view coordinates ...
model.GetWorkPlaneHandler().SetCurrentTransformationPlane(savePlane); // always restore!
```

**Safe part lookup by GUID:**
```csharp
var obj = model.SelectModelObject(new Identifier(new Guid(guid)));
var part = obj as TSM.Part;
if (part == null) return "❌ Part not found.";
```
