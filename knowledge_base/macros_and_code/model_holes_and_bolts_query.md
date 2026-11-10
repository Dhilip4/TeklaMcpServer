# Model Holes and Bolts Querying Pattern

## Purpose
Inspect, extract, and summarize holes, anchor bolt clearances, slotted holes, and bolt groups in Tekla Structures model objects (such as base plates, end plates, and structural members).

---

## 1. How Holes are Represented in the Tekla Model

In Tekla Structures Open API:
1. **Holes from Bolt Groups**:
   - Holes are usually created via `BoltGroup` (`BoltArray`, `BoltCircle`, `BoltXYList`).
   - If `bg.Bolt == false`, the object represents **pure holes only** (e.g., base plate anchor bolt holes).
   - If `bg.Bolt == true`, standard bolts with washer/nut assemblies and holes are modeled.
2. **Hole Diameters & Tolerances**:
   - `bg.BoltSize`: The nominal bolt diameter (e.g., 20.0 for M20, 24.0 for M24).
   - `bg.Tolerance`: The clearance added to the bolt size (e.g., 2.0 mm standard, 6.0 mm anchor bolt clearance).
   - **Effective Hole Diameter**: `Hole Diameter = bg.BoltSize + bg.Tolerance`.
   - Alternatively, read the report property: `bg.GetReportProperty("DIAMETER", ref double holeDia)` or `"HOLE.DIAMETER"`.
3. **Slotted & Oversized Holes**:
   - `bg.HoleType`: Enumeration (`BoltHoleTypeEnum.HOLE_TYPE_SLOTTED`, `HOLE_TYPE_OVERSIZED`, etc.).
   - Slot dimensions: `bg.SlottedHoleX` and `bg.SlottedHoleY`.
4. **Hole Quantities**:
   - Number of holes in the group: `bg.BoltPositions.Count`.

---

## 2. Querying Holes on a Part

To get all holes connected to a part:

```csharp
TSM.Part part = ...;
part.Select();

ModelObjectEnumerator boltEnum = part.GetBolts();
while (boltEnum.MoveNext())
{
    if (boltEnum.Current is TSM.BoltGroup bg)
    {
        bg.Select();
        double boltSize = bg.BoltSize;
        double tolerance = bg.Tolerance;
        double holeDia = boltSize + tolerance;
        int countInGroup = bg.BoltPositions != null ? bg.BoltPositions.Count : 0;
        bool isHoleOnly = !bg.Bolt;
        
        // Always track unique GUIDs to prevent double-counting
        string guid = bg.Identifier.GUID.ToString();
    }
}
```

---

## 3. Dedicated MCP Tool

Use the built-in MCP tool `GetHolesSummary(string guids = "")` in `TeklaMcpServer.Tools.QueryTools`:
- If `guids` is omitted, it reads whatever the user currently has selected in the Tekla UI.
- If `guids` is provided, it processes those specific objects.
- It automatically groups the results by bolt size, hole diameter, hole type, usage, and profile.

---

## 4. Anchor Bolt Templates Tool (T1, T2, T3...)

For site foundation erection and template preparation:
`GetBasePlateTemplates(string guids = "", string prefix = "T", bool groupByProfile = false)`
- Inspects selected base plates.
- Extracts `Pitch` (spacing along length) using `ba.GetBoltDistXCount()` / `ba.GetBoltDistX(i)`.
- Extracts `Gauge` (spacing along width) using `ba.GetBoltDistYCount()` / `ba.GetBoltDistY(i)`.
- Automatically assigns template identifiers (`T1`, `T2`, etc.) and calculates quantities (`T1 = __ NOS, T2 = __ NOS`).
- Outputs:
  1. High-level template quantities summary.
  2. Detailed engineering specifications (Bolt size, Hole diameter, Pitch × Gauge, and Base plate profiles).

---

## 5. Tekla Macro Compilation Rules (C# 5)

If querying via dynamic `.cs` macros using `Operation.RunMacro()`:
- **Never use C# 6+ `$"..."` string interpolation** &rarr; results in `error CS1056: Unexpected character '$'`.
- **Use `string.Format(...)` or string concatenation (`+`)**.
- **Always qualify `Tekla.Structures.Model.UI.ModelObjectSelector`** to avoid CS0104 ambiguity with `Tekla.Structures.Model.ModelObjectSelector`.
- **Use `BooleanPart`**, not `BooleanItem`.

---

## Tags
holes, bolts, baseplate, BoltGroup, BoltSize, Tolerance, anchor bolt, GetBolts, C# 5 macro

