# 🤖 Smart Clone Agent Playbook
### Tekla MCP Server — Tekla 2022.0 Compatible
> **For the AI agent.** Follow this playbook every time a user asks to clone, copy,
> or create drawings based on an existing model. Never skip a phase. Never guess a GUID.

---

## 🗺️ The 7 Tools — Quick Reference

| Tool | Purpose | When to Call |
|------|---------|-------------|
| `ScanForTeklaModels` | Finds all `.db1` model folders on disk | Once per machine, or when user adds new drives |
| `ListModelRegistry` | Shows indexed models + drawing counts | To confirm what's in the DB before fingerprinting |
| `BuildFingerprintsFromOpenModel` | Indexes drawings from the currently open Tekla model | Once per reference model |
| `GetFingerprintDatabaseStatus` | Shows total drawings indexed, per-model breakdown | To verify indexing is complete |
| `FindTop3MatchingDrawings` | Scores all fingerprints vs a target assembly GUID | Before every clone operation |
| `CloneDrawingFromMatch` | Fires the macro to clone match #1, #2, or #3 | After reviewing the top-3 output |
| `SmartCloneAllSelected` | Batch clones for all selected assemblies | When user has selected multiple assemblies in Tekla |

---

## 📋 Phase 0 — Pre-Flight Checks

> Run these **once per session** before any cloning work.

```
Step 0.1 — Confirm Tekla is running with a model open
→ Call: GetModelStatus()
→ Expected: "✅ CONNECTED! Project: ..."
→ If ❌: Stop. Tell user to open a model in Tekla first.

Step 0.2 — Check what's indexed
→ Call: GetFingerprintDatabaseStatus()
→ If "DB is empty" → Go to Phase 1 (Build the DB)
→ If count > 0 → Confirm with user: "Is this DB current? Or do you need to re-index?"
```

> [!IMPORTANT]
> Never skip `GetModelStatus()`. If Tekla isn't connected, every downstream tool fails silently.

---

## 📋 Phase 1 — Build the Fingerprint Database

> Run this **once per reference model** (old projects you want to clone FROM).

### 1A — Scan for models (first time only)

```
→ Call: ScanForTeklaModels(drives: ["C", "D"])
→ Read the output: list of model paths found
→ Call: ListModelRegistry()
→ Confirm: user sees their old project models listed
```

> [!NOTE]
> `ScanForTeklaModels` can take 1–3 minutes on large drives. Tell the user it's running.

### 1B — Index each reference model

For **each** model that contains drawings you want to clone FROM:

```
Step 1. Ask user: "Please open model [X] in Tekla now."
Step 2. Wait for user confirmation that model is open.
Step 3. Call: GetModelStatus()
        → Confirm project name matches the model they opened.
Step 4. Call: BuildFingerprintsFromOpenModel()
        → Expected: "✅ Indexed N drawings from [ModelName]"
Step 5. Repeat for each reference model.
```

### 1C — Verify database is populated

```
→ Call: GetFingerprintDatabaseStatus()
→ Report to user: "X drawings indexed across Y models."
→ If count is 0: something went wrong. Re-run Step 1B.
```

> [!TIP]
> After indexing, the user can close the old model and re-open the current working model.

---

## 📋 Phase 2 — Single Assembly Clone

> The correct flow for cloning one drawing at a time.

### Step 2.1 — Get the target GUID

```
Option A: User selects an assembly in Tekla
→ Call: GetElementsProperties()
→ Read the GUID from the output.

Option B: User pastes a GUID directly
→ Use that GUID as-is.
```

> [!IMPORTANT]
> Always use the **assembly** GUID, not a plate or bolt GUID.
> If `FindTop3` returns "Could not resolve a Beam/Column", ask for the main member's GUID.

### Step 2.2 — Find the top 3 matches

```
→ Call: FindTop3MatchingDrawings(targetGuid: "<GUID>")
→ Read the output carefully:

    Match #1  Score: 12.3 (lower = better)
    Drawing  : [A-01]  RAFTER L=12000
    Profile  : IPE500
    Model    : OldProject_2023  ✅ current model / 📂 other model — macro path
```

> [!NOTE]
> Score interpretation:
> - 0–30 → Excellent match (same profile family, close weight)
> - 31–80 → Good match (same family, some differences)
> - 81–150 → Acceptable (review before cloning)
> - > 150 → Poor match — ask user to confirm or skip

### Step 2.3 — Present matches to user

Before cloning, always show the user:

```
"I found 3 candidate drawings:

  #1 Score 12.3 — [A-01] IPE500 12.0m  from OldProject_2023  ← best
  #2 Score 34.7 — [A-03] IPE500 11.6m  from OldProject_2022
  #3 Score 67.1 — [B-02] IPE450 12.5m  from OldProject_2021

Which match should I clone? (1, 2, or 3) Or shall I proceed with #1?"
```

> [!IMPORTANT]
> Do NOT auto-clone without showing the user the matches first.
> The user must confirm which match to use, especially for unfamiliar models.

### Step 2.4 — Clone

```
→ Call: CloneDrawingFromMatch(targetGuid: "<GUID>", matchNumber: 1)
→ Expected success output:
    ✅ Drawing cloned successfully!
       Source  : [A-01]  RAFTER L=12000  from OldProject_2023
       New mark: A-47
       Open it with: OpenDrawing("A-47")
```

### Step 2.5 — Handle failures

| Error message | What to do |
|--------------|-----------|
| `❌ Fingerprint DB empty` | Go to Phase 1 — index the reference models first |
| `❌ Source model folder missing` | Ask user to connect the drive / verify path |
| `❌ Target object not found` | Use `GetElementsProperties()` to get a fresh GUID |
| `❌ RunMacro returned false` | Tell user to run macro manually (path is printed) |
| `❌ CloneDrawing returned null` | Run `NumberingSetup()` / ensure assembly is numbered |
| `⚠️ Result file not created` | Tekla may have crashed. Restart Tekla and retry |

---

## 📋 Phase 3 — Batch Clone (All Selected Assemblies)

> Use this when the user needs drawings for many assemblies at once.

### Step 3.1 — Prepare in Tekla

```
Ask user: "Please select the assemblies you want drawings for.
           You can use a Phase filter, Select → By type, or manual selection."
Wait for confirmation.
```

### Step 3.2 — Run batch clone

```
→ Call: SmartCloneAllSelected(scoreThreshold: 150)
→ Read the output:

    📋 Batch Smart Clone — complete
       ✅  Cloned  : 14
       ⏭️  Skipped : 3   (already had drawings)
       ❌  Failed  : 2

    ✅  [IPE500] RAFTER-A1 → score 12.3
    ✅  [HEA300] COLUMN-B2 → score 8.7
    ⚠️  [RHS200x10] PURLIN → no match below threshold (best: 167)
    ❌  [PL20] BASEPLATE → clone returned null
```

### Step 3.3 — Handle partial failures

```
For each ❌ line in batch output:
1. Extract the profile name.
2. Ask user: "PURLIN had no good match. Do you want to:
              a) Create a drawing from scratch
              b) Choose from a manual list
              c) Skip it for now"
3. For assemblies with no drawings, offer to re-run
   FindTop3MatchingDrawings() one-by-one.
```

> [!TIP]
> Lower `scoreThreshold` (e.g., 80) for strict projects where bad clones are worse than no drawings.
> Raise it (e.g., 200) when you want maximum coverage and will review manually.

---

## 📋 Phase 4 — Post-Clone Verification

```
Step 4.1 — List the new drawings
→ Call: ListDrawings()
→ Verify new drawings appear in the list.

Step 4.2 — Open and visually check one
→ Call: OpenDrawing("<new mark>")
→ Tell user: "Please visually verify the cloned drawing is correct."

Step 4.3 — Optional: export to PDF for review
→ Call: ExportToPdf("<new mark>")
→ Report the PDF path to the user.
```

---

## 🔁 Complete Session Flow (Cheat Sheet)

```
NEW SESSION
    ↓
GetModelStatus()               ← is Tekla connected?
    ↓
GetFingerprintDatabaseStatus() ← is DB populated?
    ↓ (if empty)
ScanForTeklaModels()           ← find old models on disk
ListModelRegistry()            ← confirm they're found
[user opens reference model]
BuildFingerprintsFromOpenModel() ← index it
[repeat per reference model]
GetFingerprintDatabaseStatus() ← confirm DB is populated
    ↓
[user opens CURRENT working model]
GetElementsProperties()        ← get target GUID
    ↓
FindTop3MatchingDrawings(guid) ← show user the matches
    ↓ (user confirms)
CloneDrawingFromMatch(guid, 1) ← clone it
    ↓
OpenDrawing("new mark")        ← verify result
    ↓ (for batches)
SmartCloneAllSelected()        ← batch remainder
```

---

## ⚠️ Critical Rules — Never Break These

> [!CAUTION]
> **Rule 1 — Always confirm Tekla is connected first.**
> Every call silently fails if Tekla has no open model.

> [!CAUTION]
> **Rule 2 — Never clone without showing the user the top-3 first.**
> A wrong clone is worse than no drawing — it takes time to fix.

> [!CAUTION]
> **Rule 3 — Never guess or fabricate a GUID.**
> Always call `GetElementsProperties()` to get a real GUID from the live model.

> [!WARNING]
> **Rule 4 — Re-index when the reference model changes.**
> If drawings were added/modified in a reference model, run `BuildFingerprintsFromOpenModel()` again.

> [!WARNING]
> **Rule 5 — Score > 150 = don't auto-clone.**
> Ask the user for guidance. A poor match produces a misleading drawing.

> [!NOTE]
> **Rule 6 — The macro writes to `AGENT_MEMORY\macro_result.txt`.**
> If the file doesn't appear in 25s, Tekla may be busy or frozen. Tell the user.

---

## 🗣️ User Communication Templates

**Starting indexing:**
> "I'll now index the drawings from *[ModelName]*. This scans all assembly drawings and stores their fingerprints. It takes about 10–30 seconds. Stand by..."

**Presenting top-3:**
> "I found the 3 best matches for this assembly (profile: *IPE500*, weight: *234 kg*):
> - **#1** `[A-01]` from *OldProject_2023* — Score **12.3** ✅ excellent
> - **#2** `[A-03]` from *OldProject_2022* — Score **34.7** 👍 good
> - **#3** `[B-02]` from *OldProject_2021* — Score **67.1** ⚠️ review needed
>
> Shall I proceed with match #1?"

**Macro running:**
> "Firing the clone macro inside Tekla now. Waiting for result (up to 25 seconds)..."

**After success:**
> "✅ Done! Drawing **A-47** was cloned from *[A-01]* in *OldProject_2023*.
> I'll open it now — please verify it looks correct."

**After partial batch failure:**
> "Batch complete: 14 cloned, 3 skipped (already had drawings), 2 failed.
> The 2 failures were: *[RHS200x10] PURLIN* and *[PL20] BASEPLATE*.
> Do you want me to find matches for these individually?"
