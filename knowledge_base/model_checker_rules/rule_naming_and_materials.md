# Rule: Part Naming Convention

## Category
Naming

## Severity
Warning

## Check
All model parts must follow the company naming convention.

## Standard Names
- **Columns**: `COLUMN` or `COL`
- **Rafters**: `RAFTER` or `RAF`
- **Bracing**: `BRACE` or `BRACING`
- **Purlins**: `PURLIN` or `PUR`
- **Girts**: `GIRT`
- **Base Plates**: `BASE PLATE` or `BP`
- **End Plates**: `END PLATE` or `EP`
- **Stiffeners**: `STIFFENER` or `STIFF`
- **Handrails**: `HANDRAIL` or `HR`

## Pass Condition
Part.Name matches one of the standard names (case-insensitive).

## Fail Message
"Part {GUID} has non-standard name '{Name}'. Expected one of: COLUMN, RAFTER, BRACE, PURLIN, GIRT, BASE PLATE, END PLATE, STIFFENER, HANDRAIL."

## How to Check via MCP
```
1. SelectElementsByFilter(element_type: "BEAM")
2. GetElementsProperties() → Read Name field
3. Compare against standard names list
4. Report violations
```

## Reference
Company Standard - Naming Convention Rev.A

---

# Rule: Material Grade Compliance

## Category
Material

## Severity
Error

## Check
All steel parts must use approved material grades.

## Approved Grades
- `S235JR`
- `S275JR`
- `S355JR`
- `S355J2`
- `S450J0`

## Pass Condition
Part.Material is in the approved grades list.

## Fail Message
"Part {GUID} uses unapproved material '{Material}'. Must be one of: S235JR, S275JR, S355JR, S355J2, S450J0."

## How to Check via MCP
```
1. SelectElementsByFilter(element_type: "BEAM")
2. GetElementsProperties() → Read Material field
3. Compare against approved list
4. Report violations
```

## Reference
Company Standard - Material Specification Rev.B
