# Bolt Filtering Pattern in Drawings

## Purpose
Filter and select specific bolts in a Tekla drawing by name (which contains size and length info).

## How Bolt Names Work in Drawings
When you read the `NAME` report property from a drawing bolt group object, it returns a string like:
- `M24×80 8.8 4 BOLT`
- `M20×90 10.9 6 BOLT`
- `M16×50 8.8 2 BOLT`

The format is: `{Size}×{Length} {Grade} {Count} BOLT`

## Filtering Algorithm
```csharp
// Get all objects from all views in the active drawing
foreach (TSD.View view in allViews)
{
    var drawingObjects = view.GetAllObjects(typeof(TSD.BoltGroup));
    while (drawingObjects.MoveNext())
    {
        if (drawingObjects.Current is TSD.BoltGroup bg)
        {
            string name = "";
            bg.GetReportProperty("NAME", ref name);
            
            // Check against filter patterns (supports wildcards with *)
            if (MatchesFilter(name, filterPatterns))
            {
                boltsToSelect.Add(bg);
            }
        }
    }
}
```

## Filter Syntax
Pass comma-separated patterns with `*` wildcards:
- `M24*80` — All M24 bolts with 80mm length
- `M20*90,M16*50` — M20×90 and M16×50 bolts
- `M24*` — All M24 bolts regardless of length

## Complete Workflow
1. Filter bolts by name pattern
2. Select filtered bolts via `DrawingObjectSelector.SelectObjects()`
3. Trigger `ADD-PART.cs` macro to apply marks

## Tags
bolt, filter, drawing, NAME, BoltGroup, selection, marks
