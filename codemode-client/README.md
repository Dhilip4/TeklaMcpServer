# Tekla Code Mode — MCP Client Wrapper (Vercel AI SDK)

This document is the authoritative usage guide for **both AI agents and human operators** working with the Tekla Code Mode wrapper. It explains what the system is, how to install and run it, how to configure it, how to write effective prompts, what Tekla tools are available, how errors are handled, and how to troubleshoot common problems.

---

## 1. Overview

The **Tekla Code Mode wrapper** is a Next.js / Vercel AI SDK application that sits between an AI agent (Claude, GPT-5, etc.) and your existing **TeklaMcpServer** (.NET 4.8, stdio transport). It implements the **Code Mode** pattern:

1. The agent writes a short JavaScript/TypeScript program inside an isolated QuickJS sandbox.
2. That program can call any of the 84+ Tekla MCP tools exposed by TeklaMcpServer.
3. Only the final JSON-serializable result is returned to the agent's context window — intermediate tool noise is stripped out.

This means you can chain multi-step Tekla workflows (select → inspect → modify → commit → query) in a single model turn, while keeping the underlying TeklaMcpServer completely unchanged.

```
┌─────────────────┐     ┌─────────────────────────────────────┐     ┌──────────────────┐
│  AI Agent       │────▶│  Next.js + Vercel AI SDK (Code Mode)│────▶│  TeklaMcpServer  │
│  (Claude / GPT) │     │  • code_mode tool (QuickJS sandbox) │     │  (.NET 4.8, stdio)│
└─────────────────┘     │  • experimental_toolCallers restrict│     │  • Tekla Open API  │
                        │    all Tekla tools to code_mode     │     │  • Tekla 2025.0    │
                        └─────────────────────────────────────┘     └──────────────────┘
```

### Why Code Mode?

| Problem | How Code Mode solves it |
|---|---|
| 84+ individual MCP tools bloat the agent's context window | Exposed as a **single** `code_mode` tool; the underlying Tekla tool catalog is announced in-conversation |
| Multi-step Tekla workflows require many round-trips | The agent writes one JS script that loops/filters/chains Tekla calls inside the sandbox |
| Large tool outputs (e.g. all element properties) waste tokens | Only the final return value comes back to the LLM; intermediate data stays in the sandbox |

---

## 2. Project Structure (codemode-client)

```
codemode-client/
├── README.md                      # This file
├── package.json                   # Node dependencies (ai@7, @ai-sdk/code-mode, etc.)
├── tsconfig.json                  # TypeScript configuration
├── next.config.mjs                # Next.js configuration
├── .env.local                     # Environment variables (API keys, paths)
├── app/
│   ├── layout.tsx                 # Root layout + metadata
│   ├── page.tsx                   # Chat UI (connection, messages, send form)
│   └── api/
│       └── chat/
│           └── route.ts           # Core API: stdio MCP client + Code Mode wrapper
└── node_modules/                  # Installed npm packages (created by `npm install`)
```

**Important:** `node_modules/` is **not** created until you run `npm install`. Do not commit it.

## 3. Prerequisites

Before you start, verify the following are installed on your machine:

| Dependency | Minimum Version | How to Check |
|---|---|---|
| .NET SDK | 10.0.x (already present: 10.0.400) | `dotnet --version` |
| Tekla Structures | 2025.0 (DLLs at `C:\TeklaStructures\2025.0\bin`) | Check folder exists |
| TeklaMcpServer build | `bin\Debug\net48\TeklaMcpServer.exe` | Check file exists |
| Node.js | v22.x (present: v22.23.2) | `node --version` |
| npm | v12.x (present: 12.0.2) | `npm --version` |

### TeklaMcpServer must be built

The wrapper launches the existing compiled TeklaMcpServer executable. Confirm it exists:

```
d:\Dhilip\Cod\TeklaMcpServer\bin\Debug\net48\TeklaMcpServer.exe
```

If it does not exist, build the server first from the .NET project:

```powershell
cd d:\Dhilip\Cod\TeklaMcpServer
dotnet build TeklaMcpServer.csproj
```

### Tekla Structures must be running

The MCP server connects to a **live** Tekla Structures 2025.0 instance with a model open. Start Tekla, open a model, then launch the wrapper.



## 4. Installation

From the `codemode-client` directory:

```powershell
cd d:\Dhilip\Cod\TeklaMcpServer\codemode-client
npm install --legacy-peer-deps
```

This installs:

| Package | Version | Role |
|---|---|---|
| `ai` | 7.0.166 | Vercel AI SDK core (`generateText`, tools) |
| `@ai-sdk/code-mode` | 1.0.123 | QuickJS-backed Code Mode sandbox tool |
| `@ai-sdk/mcp` | 0.0.10 | MCP client (`createMCPClient`) |
| `@ai-sdk/mcp/mcp-stdio` | 0.0.10 | Stdio transport (`StdioMCPTransport`) |
| `@ai-sdk/anthropic` | 4.0.99 | Anthropic model provider |
| `next` | 15.0.0 | React framework / API routes |
| `react` | 19.0.0 | UI library |
| `zod` | 3.23.0 | Request validation |

> **Note:** `--legacy-peer-deps` is required because Next.js 15 expects React 18-compatible peer ranges, but React 19 is installed. This is safe and expected.

## 5. Configuration (`.env.local`)

Edit `codemode-client/.env.local` before first use. The file is pre-created with defaults; you **must** add your API key.

```env
# ============================================================
# Tekla MCP Server (stdio) paths
# ============================================================
TEKLA_MCP_EXE_PATH=d:\Dhilip\Cod\TeklaMcpServer\bin\Debug\net48\TeklaMcpServer.exe
TEKLA_BIN_DIR=C:\TeklaStructures\2025.0\bin

# ============================================================
# AI Model Provider
# ============================================================
AI_MODEL_PROVIDER=anthropic          # anthropic (default) or openai
ANTHROPIC_MODEL=claude-sonnet-4-20250514
# OPENAI_MODEL=gpt-5

# ============================================================
# API Keys — REQUIRED. Uncomment and set your actual key.
# ============================================================
# ANTHROPIC_API_KEY=sk-ant-your-key-here
# OPENAI_API_KEY=sk-your-key-here
```

| Variable | Required | Description |
|---|---|---|
| `TEKLA_MCP_EXE_PATH` | Yes | Full path to the compiled TeklaMcpServer.exe |
| `TEKLA_BIN_DIR` | Yes | Tekla Structures bin directory (passed to the server process) |
| `AI_MODEL_PROVIDER` | No | `anthropic` (default) or `openai` |
| `ANTHROPIC_MODEL` | No | Anthropic model name (default: `claude-sonnet-4-20250514`) |

## 6. Running the Wrapper

### Development mode

```powershell
cd d:\Dhilip\Cod\TeklaMcpServer\codemode-client
npm run dev
```

The app starts on `http://localhost:3000`. Open that URL in your browser.

### Production build

```powershell
cd d:\Dhilip\Cod\TeklaMcpServer\codemode-client
npm run build
npm start
```

### Startup checklist

Before sending your first message, confirm:

1. ☐ Tekla Structures 2025.0 is running with a model open
2. ☐ `TeklaMcpServer.exe` exists at the path in `.env.local`
3. ☐ Your API key is set in `.env.local`
4. ☐ `npm run dev` is running and the page loads
5. ☐ The connection indicator shows **Connected** (green dot)

The connection indicator checks `GetModelStatus()` on page load. If it shows red, Tekla or the MCP server is not reachable.

## 7. How It Works (Technical Details)

### 7.1 The API route (`app/api/chat/route.ts`)

Every chat message triggers `POST /api/chat`. The route:

1. **Parses** the user prompt from the request body.
2. **Launches** TeklaMcpServer via `StdioMCPTransport` (stdio stdin/stdout).
3. **Connects** an MCP client (`experimental_createMCPClient`) and calls `mcpClient.tools()` to discover all 84+ auto-discovered Tekla tools.
4. **Restricts** every discovered Tekla tool so that only `code_mode` can invoke it:
   ```ts
   const toolCallers = Object.fromEntries(
     Object.keys(mcpTools).map((toolName) => [toolName, ['code_mode']])
   );
   ```
5. **Calls** `generateText` with:
   - `model`: Anthropic Claude (or OpenAI)
   - `tools`: `{ code_mode: codeModeTool({ toolDiscovery: 'conversation' }), ...mcpTools }`
   - `experimental_toolCallers`: the restriction map above
   - `prompt`: the user's natural-language request
6. **Returns** only `result.text` to the client.

### 7.2 Code Mode sandbox behavior

The model is given a single `code_mode` tool whose input is a string of JavaScript/TypeScript source. Inside the QuickJS sandbox:

- The global `tools` object contains every Tekla MCP tool as an async function.
- The model can write:
  ```js
  const status = await tools.GetModelStatus();
  const beams = await tools.ModelBeam({ profile: "HEA300", material: "S355JR", x1: 0, y1: 0, z1: 0, x2: 6000, y2: 0, z2: 0 });
  const props = await tools.GetElementsProperties();
  return { status, beams, props };
  ```
- Only the `return` value is sent back to the LLM. All intermediate console output, tool call metadata, and large arrays are filtered by the sandbox execution policy (default `maxResultBytes: 2MB`).


## 8. Usage Examples (Agent Prompts)

Below are example natural-language prompts you can type into the chat UI. The agent translates them into Code Mode scripts that chain Tekla tools.

### Example 1: Create a beam and column, then inspect them

> "Create a steel beam HEA300 from (0,0,0) to (6000,0,0) using material S355JR, then create a column HEA300 at the end point going up 4000mm, and return the GUIDs and properties of both objects."

Expected internal Code Mode script (simplified):
```js
const beam = await tools.ModelBeam({
  profile: "HEA300", material: "S355JR",
  x1: 0, y1: 0, z1: 0, x2: 6000, y2: 0, z2: 0
});
const column = await tools.ModelBeam({
  profile: "HEA300", material: "S355JR",
  x1: 6000, y1: 0, z1: 0, x2: 6000, y2: 0, z2: 4000
});
const props = await tools.GetElementsProperties();
return { beam, column, props };
```

### Example 2: Select all columns and get their positions

> "Select all elements with class '1' and profile starting with 'HEA', then give me their GUIDs, start points, and end points."

### Example 3: Modify multiple parts by GUID

> "Change the profile of the parts with GUIDs abc-123 and def-456 to HEA400 and material to S420ML."

### Example 4: Base plate anchor bolt templates

> "I have selected some base plates in Tekla. Get me the anchor bolt hole templates grouped by pitch and gauge, like T1 = 129 NOS, T2 = 4 NOS."

This maps to `GetBasePlateTemplates()` / `GetHolesSummary()` in QueryTools.

### Example 5: Create drawings

> "Create a GA drawing named 'FRAME CROSS SECTION AT G.L - 8' from the named model view, with view depth up 650 and down 650."

This maps to `CreateGADrawing()` in DrawingTools.

### Example 6: Complex multi-step workflow

> "Find all beams that clash with column C-01, then apply an end plate connection (component 144) to each beam-column pair, then create assembly drawings for the affected assemblies."

This chains `CheckClash()`, `AddConnection()`, and `CreateAssemblyDrawing()` across multiple tool calls inside one sandbox script.

### 7.3 Transport choice: stdio only

Your TeklaMcpServer is **stdio-only** (`WithStdioServerTransport()`). The wrapper uses `@ai-sdk/mcp/mcp-stdio`'s `StdioMCPTransport` (exported as `Experimental_StdioMCPTransport`) to spawn `TeklaMcpServer.exe` as a child process. No changes to the .NET server are required.

| `ANTHROPIC_API_KEY` | **Yes** | Your Anthropic API key |
| `OPENAI_API_KEY` | **Yes** | Your OpenAI API key (only if using OpenAI provider) |

> **Security:** `.env.local` is gitignored by default and should never be committed to version control. It contains secret API keys.


## 9. Available Tekla Tools (84+ total)

The wrapper exposes **all** tools auto-discovered from TeklaMcpServer. They are grouped by category below. Every tool is restricted to `code_mode` only — the agent cannot call them directly.

| Category | File | Tool Count | Key Tools |
|---|---|---|---|
| Model Operations | `Tools/ModelTools.cs` | 26 | `ModelBeam`, `ModelContourPlate`, `ModelBoltArray`, `ModelBoltCircle`, `AddConnection`, `ApplyCut`, `ApplySurfaceTreatment`, `PlaceRebar`, `MoveParts`, `CopyParts`, `SplitBeam`, `MergeBeams` |
| Drawing Lifecycle | `Tools/DrawingTools.cs` | 18 | `ListDrawings`, `GetSelectedDrawings`, `OpenDrawing`, `SaveActiveDrawing`, `CreateAssemblyDrawing`, `CreateSinglePartDrawing`, `CreateGADrawing`, `BatchUpdateDrawings` |
| Drawing Details | `Tools/DrawingDetailTools.cs` | 16 | `AddPartMarks`, `AddBoltMarks`, `AddWeldMarks`, `AddDimensions`, `AddTextNote`, `InsertDWG`, `CreateSectionView`, `CreateDetailView` |
| Selection & Query | `Tools/SelectionTools.cs` | 7 | `SelectElementsByGuid`, `SelectElementsByFilter`, `GetElementsProperties`, `GetAllElementsUDAs`, `SetElementsUDAs` |
| Query & Inspection | `Tools/QueryTools.cs` | — | `GetHolesSummary`, `GetBasePlateTemplates`, `ModifyParts`, `GetModelStatus`, `GetProjectInfo`, `GetModelExtents` |
| Core | `Tools/CoreTools.cs` | — | `CommitChanges`, `DeleteObject`, `GetProjectInfo`, `GetModelExtents` |
| Reports & Export | `Tools/ExportTools.cs` | 4 | Export reports, NC files, MIS export |
| Macros & UI | `Tools/MacroTools.cs` | 3 | `RunTeklaMacro`, `IsMacroRunning`, `DisplayPrompt` |
| Views & Grids | `Tools/ViewTools.cs` | 2 | `ListModelViews`, `GenerateGrid` |
| Phases | `Tools/PhaseTools.cs` | 1 | `AutoAssignPhase` |
| Standalone | `FingerprintEngine.cs`, `ModelScanner.cs`, `SmartClone.cs` | 7 | `BuildFingerprintsFromOpenModel`, `FindTop3MatchingDrawings`, `CloneDrawingFromMatch`, `ScanForTeklaModels` |

**Total:** 77 modular + 7 standalone = **84 tools**

Every tool returns a string with an emoji prefix:
- `✅` — Success
- `❌` — Expected failure (retry with corrected parameters)
- `⚠️` — Error/exception
- `⏳` — In progress (macros)


## 10. Error Handling Conventions

All Tekla tools follow the contract defined in `ARCHITECTURE.md` and `important_instructions_to_create_tool.md`:

1. **Create a fresh `new Model()` per tool call** — never cache the model object.
2. **Call `CommitChanges()` after any Insert/Modify/Delete.**
3. **Write diagnostics to `Console.Error` only** — stdout is reserved for MCP JSON-RPC.
4. **Use namespace aliases** (`TSM = Tekla.Structures.Model`, `TSD = Tekla.Structures.Drawing`) to avoid type ambiguity.
5. **Return emoji-prefixed strings** (`✅`/`❌`/`⚠️`/`⏳`).

If a tool returns `⚠️ Error: ...`, the agent should:
1. Read the error message.
2. Correct the parameters (e.g., verify profile/material strings match the Tekla catalog).
3. Retry the operation inside a new Code Mode script.

Common errors and fixes:

| Error | Cause | Fix |
|---|---|---|
| `❌ No active model connection.` | Tekla not running or model not open | Start Tekla, open a model, retry |
| `❌ No active drawing open.` | No drawing is opened | Call `OpenDrawing(mark)` first |
| `❌ Part not found.` | Invalid GUID | Re-select parts, get fresh GUIDs |
| `⚠️ Modify failed.` | Profile/material not in catalog | Verify against Tekla catalog |
| `⚠️ Error: ...` | Unexpected exception | Check Tekla logs, simplify the operation |

## 11. Security Considerations

| Concern | Mitigation |
|---|---|
| **Destructive tools** (`DeleteObject`) are exposed to `code_mode` | Filter `Object.keys(mcpTools)` in `route.ts` if you want to block them |
| **API keys in `.env.local`** | Never commit `.env.local`; restrict file permissions |
| **Result size** | Code Mode defaults `maxResultBytes` to 2MB; tighten via `CodeModeExecutionPolicy` if needed |
| **Arbitrary JS execution** | The sandbox runs model-written code; do not expose this wrapper to untrusted users without review |
| **Stdout pollution** | Tekla tools write to `Console.Error`; the MCP JSON-RPC on stdout remains clean |


## 12. Troubleshooting

### Problem: Connection indicator stays red

| Check | Command / Action |
|---|---|
| Tekla running? | Open Tekla Structures 2025.0 with a model loaded |
| EXE path correct? | Verify `TEKLA_MCP_EXE_PATH` in `.env.local` points to a real file |
| Tekla bin dir correct? | Verify `TEKLA_BIN_DIR=C:\TeklaStructures\2025.0\bin` |
| Server builds? | Run `dotnet build` in the .NET project |

### Problem: `npm install` fails with peer dependency conflicts

Use `--legacy-peer-deps`:
```powershell
npm install --legacy-peer-deps
```

### Problem: Build fails with `Unknown compiler option 'modules'`

Ensure `tsconfig.json` uses `"module": "ESNext"` (not `"modules"`).

### Problem: Tool call fails with `StdioMCPTransport` import error

Ensure you imported it as:
```ts
import { Experimental_StdioMCPTransport as StdioMCPTransport } from '@ai-sdk/mcp/mcp-stdio';
```

### Problem: Large outputs truncate the context

Wrap the return value in the Code Mode script to summarize instead of returning raw arrays:
```js
return { count: parts.length, summary: parts.map(p => ({ guid: p.GUID, profile: p.Profile })) };
```

## 13. Customizing the Wrapper

### Block destructive tools

Edit `app/api/chat/route.ts`, replace the `toolCallers` construction:

```ts
const blockedTools = ['DeleteObject'];
const toolNames = Object.keys(mcpTools).filter(t => !blockedTools.includes(t));
const toolCallers = Object.fromEntries(toolNames.map((toolName) => [toolName, ['code_mode']]));
```

### Add result-size caps

Pass `executionPolicy` to `codeModeTool`:

```ts
codeModeTool({
  toolDiscovery: 'conversation',
  executionPolicy: {
    maxResultBytes: 512 * 1024,   // 512 KB cap
    timeoutMs: 60000,             // 60 second wall-clock limit
  },
})
```

### Switch to OpenAI

```env
AI_MODEL_PROVIDER=openai
OPENAI_MODEL=gpt-5
OPENAI_API_KEY=sk-your-key-here
```

Then update `route.ts` to conditionally import the OpenAI provider (currently hardcoded to Anthropic).

## 14. Files You Created (Summary)

| Path | Description |
|---|---|
| `d:\Dhilip\Cod\TeklaMcpServer\codemode-client\README.md` | This guide |
| `d:\Dhilip\Cod\TeklaMcpServer\codemode-client\package.json` | Dependencies & scripts |
| `d:\Dhilip\Cod\TeklaMcpServer\codemode-client\app\api\chat\route.ts` | Core MCP + Code Mode wrapper logic |
| `d:\Dhilip\Cod\TeklaMcpServer\codemode-client\app\page.tsx` | Chat UI |
| `d:\Dhilip\Cod\TeklaMcpServer\codemode-client\app\layout.tsx` | Root layout |
| `d:\Dhilip\Cod\TeklaMcpServer\codemode-client\.env.local` | Environment configuration |
| `d:\Dhilip\Cod\TeklaMcpServer\codemode-client\next.config.mjs` | Next.js config |
| `d:\Dhilip\Cod\TeklaMcpServer\codemode-client\tsconfig.json` | TypeScript config |

## 15. Quick Start Checklist

1. [ ] Tekla Structures 2025.0 is running with a model open
2. [ ] `TeklaMcpServer.exe` is built at the expected path
3. [ ] `cd d:\Dhilip\Cod\TeklaMcpServer\codemode-client`
4. [ ] `npm install --legacy-peer-deps`
5. [ ] Add your API key to `.env.local`
6. [ ] `npm run dev`
7. [ ] Open `http://localhost:3000`
8. [ ] Confirm green "Connected" indicator
9. [ ] Type a Tekla command and send

## 16. Support

If the wrapper fails to connect:
1. Check the browser console and terminal output for errors.
2. Verify TeklaMcpServer runs standalone (start it manually and check for fatal logs).
3. Confirm the Tekla version in `config/settings.json` matches your installed Tekla (`2025.0`).

For changes to the underlying Tekla tools themselves, edit the `.cs` files in `d:\Dhilip\Cod\TeklaMcpServer\Tools/` and rebuild the .NET project — the wrapper will automatically pick up new tools on the next request because it calls `mcpClient.tools()` fresh each time.
