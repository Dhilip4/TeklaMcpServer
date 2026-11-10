# Drawing Macro Execution via API

## The Breakthrough
`Operation.RunMacro()` CAN execute drawing macros if you use relative path traversal.

## The Problem
Tekla's `Operation.RunMacro()` internally resolves paths from the `modeling` macro folder:
- `C:\TeklaStructures\2022.0\environments\common\macros\modeling\`

If you pass `"drawings\ADD-PART.cs"`, Tekla looks for:
- `...\macros\modeling\drawings\ADD-PART.cs` → **DOES NOT EXIST** → Returns `false`

## The Solution
Use `..\ ` to step out of `modeling` and into `drawings`:

```csharp
// ❌ FAILS — Tekla looks inside modeling folder
Operation.RunMacro(@"drawings\ADD-PART.cs");        // Returns false
Operation.RunMacro(@"ADD-PART.cs");                  // Returns false

// ✅ WORKS — Steps up from modeling, into drawings
Operation.RunMacro(@"..\drawings\ADD-PART.cs");      // Returns true!
```

## Proven Use Cases

### 1. Adding Applied Part Marks (acmdCreateAppliedMarksSelected)
```csharp
// Select bolts/parts first
handler.GetDrawingObjectSelector().SelectObjects(boltsToSelect, true);
// Then trigger the native drawing macro
Operation.RunMacro(@"..\drawings\ADD-PART.cs");
```
This creates TRUE Applied Marks (not associative notes).

### 2. Arranging Marks (acmdArrangeMarks)
Create a custom macro file `ArrangeMarks.cs` in `macros\drawings\`:
```csharp
namespace UserMacros {
    public sealed class Macro {
        [Tekla.Macros.Runtime.MacroEntryPointAttribute()]
        public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime) {
            var akit = runtime.Get<Tekla.Macros.Akit.IAkitScriptUtilities>();
            akit.Callback("acmdArrangeMarks", "", "main_frame");
        }
    }
}
```
Then trigger via: `Operation.RunMacro(@"..\drawings\ArrangeMarks.cs");`

## Key Principle
Any Akit command (`acmd*`) can be wrapped in a tiny `.cs` macro file, placed in `macros\drawings\`, and triggered via the API using the `..\ ` relative path. This unlocks the ENTIRE drawing automation surface.

## Known Akit Commands
- `acmdCreateAppliedMarksSelected` — Add part/bolt marks
- `acmdArrangeMarks` — Arrange marks to avoid collisions
- `acmdCreateAssociativeNoteSelected` — Add associative notes
- `acmdCreateDetailView` — Create detail views
- `acmdCreatePartMarksSelected` — Add generic part marks

## Tags
drawing, macro, RunMacro, Operation, Akit, acmd, marks, automation, ADD-PART
