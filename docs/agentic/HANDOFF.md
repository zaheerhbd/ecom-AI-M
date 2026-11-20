# HANDOFF: AI agent for eCOM-AI-M

> **Last updated:** 2026-11-16 23:21 EST
> **Status:** Phase 1 (agent engine) is built and tested (38/38), and **waiting for the owner's code review**. Branch `feature/agentic-ai`, **nothing committed** (the new work is all untracked files, see §2a).
> **Next action:** wait for the owner to (1) review the Phase 1 code and [reports/phase-1-report.html](reports/phase-1-report.html), (2) confirm D5/D6 and answer D1–D3, and (3) say "start Phase 2". Do **not** start Phase 2, commit, or touch the API until they say so.

This is the **single entry point** for any new session, whether Claude Code, Codex or another assistant. Read it fully before doing anything, then read the docs it points to. **Update it at the end of every session**: the "Last updated" line, Status, Next action and the session log at the bottom.

---

## 1. Working rules (from the owner, must follow)

1. **Phases.** Docs first (Phase 0). After that, **wait**: a phase starts only when the owner says so. Never start the next phase on your own.
2. **Code review after any coding.** Stop, list every file you changed, and wait for approval before continuing.
3. **Git:** commit or push **only when asked**. Once coding starts, work on the branch `feature/agentic-ai` (create it from `main`). End commit messages with the attribution line your tool requires.
4. **Leave the owner's own changes alone.** These are uncommitted and belong to the owner: `API/Startup.cs`, `Dockerfile`, `API/logs/`, `CLAUDE.md`, `tmp/`, `docs/container-app-github-deploy.md`.
5. **Don't add `.claude/settings.local.json` to `.gitignore`.** The owner said no.
6. **Code style for the new Agent project:** classic **C# 7.3 syntax** on the modern runtime (`<LangVersion>7.3</LangVersion>`, `<Nullable>disable</Nullable>`, `<ImplicitUsings>disable</ImplicitUsings>`). The code is commented in plain English for a learner. See `C:\Agents\SimpleAgent\CLAUDE.md` for the full rules.
7. **Ask before anything outward-facing or paid:**
   - anything that touches the **cloud database**, **Azure AI Search**, the Container App or GitHub;
   - any paid API call beyond normal testing (e.g. LLM-written descriptions, image generation).
8. **Never sync the Azure Search indexes from the laptop.** There are no dev indexes (D7), so the only sync is the production one in Phase 6.
9. **Explain in plain, easy English.** The owner is learning. Give a recommendation, not a long list of options.
10. **HTML report at the end of every phase.** Write `docs/agentic/reports/phase-N-report.html`: one self-contained page (no external files), easy to read, with a table of contents to jump around. It explains in plain English what was built, how it works, how to check it, what changed from the plan, and what's next. Copy the layout of [reports/phase-1-report.html](reports/phase-1-report.html) and link the report in [06-phase-tracker.md](06-phase-tracker.md).

## 2. Where everything is

| What | Where |
|---|---|
| Repo (git root) | `C:\eCOM-AI-M\eCOM-AI-M` (git needs `-c safe.directory=*`; a stray git repo exists at `C:\`) |
| Agent docs | `docs/agentic/` |
| Engine to copy | `C:\Agents\SimpleAgent\src\SimpleAgent.Core` (net8.0, C# 7.3, no NuGet) |
| SimpleAgent evals + UI to copy ideas from | `C:\Agents\SimpleAgent\evals\SimpleAgent.Evals`, `C:\Agents\SimpleAgent\src\SimpleAgent.Api\wwwroot\index.html` |
| Repo overview for assistants | `CLAUDE.md` in the repo root (commands, architecture) |
| Old DB backup | `C:\Users\zahee\AppData\Local\Temp\claude\69ff53a2b09b35ab\ec79f887-0a09-4e67-97e1-289e527a4481\scratchpad\skinet-backup-before-agent.sql`. This folder is **temporary**: take a fresh backup into a permanent folder before Phase 2 |

## 2a. Resume here (state at the end of the last session)

**Uncommitted work that belongs to the agent project** (keep it; commit only when asked):

| Path | What |
|---|---|
| `Agent/` (untracked) | The engine: `AgentRunner.cs` (main loop), `AgentBuilder.cs`, `ServiceCollectionExtensions.cs` (`AddAgent(services, config)`), `Guardrails/`, `Llm/` (Fake + OpenAI Chat Completions), `Memory/`, `Observability/`, `Tools/`, `Runs/`, `Evals/` |
| `Agent.Tests/` (untracked) | xUnit tests, 38 total: AgentRunner 17, ToolAndSchema 8, Eval 11, LlmAndTelemetry 2. `TestHost.cs` has the test setup and test tools |
| `eCom.sln` (modified) | Two project lines added by `dotnet sln add`. This is ours, not the owner's |
| `docs/agentic/` (untracked) | All agent docs plus `reports/phase-1-report.html` |

Everything else in `git status` (`API/Startup.cs`, `Dockerfile`, `API/logs/`, `CLAUDE.md`, `tmp/`, `docs/container-app-github-deploy.md`) is the **owner's**. Leave it alone (rule 4).

**Check it still works** (free, no network, uses the fake model):

```powershell
dotnet build eCom.sln            # expect 0 errors; 5 old warnings in API code only
dotnet test Agent.Tests          # expect Passed: 38, Failed: 0
```

**Loose ends from Phase 1:**
- The API was **not** started after Phase 1, because Docker Desktop was off. The API has no changes, but run `docker-compose up -d`, then `cd API; dotnet run` once to confirm.
- D5 (test project) and D6 (Chat Completions) were done as recommended. They still need the owner's OK.
- The engine isn't wired into the API yet. That's Phase 3, and the extra Phase 3 steps are in [02-implementation-plan.md](02-implementation-plan.md):
  - `Program.cs` `--evals`;
  - Dockerfile COPY lines (**ask the owner first**, since the Dockerfile has their uncommitted edits);
  - the new tool signature.
- There is no Admin role in identity yet. D4 needs one in Phase 3/4.
- `appsettings.json` is gitignored. Put new config examples in the docs, not only in that file.

**If the owner asks for review changes:** edit, then re-run the build and tests, and update the Phase 1 report and tracker if anything they describe changes. Stop again for review.

**When the owner says "start Phase 2":** before touching the DB, take a fresh backup into a **permanent** folder (the old one is in a temp folder, see §2). Then follow Phase 2 in doc 02. End the phase with `reports/phase-2-report.html` (rule 10).

## 3. Docs (read in this order)

| # | File | What's in it |
|---|---|---|
| 01 | [01-brd.md](01-brd.md) | Requirements R1–R12, out of scope, success criteria S1–S9, risks, open questions |
| 02 | [02-implementation-plan.md](02-implementation-plan.md) | Target structure, numbered steps for Phases 1–6, config sketch |
| 03 | [03-workflow.md](03-workflow.md) | Mermaid diagrams (loop, layers, guardrails, sign-in gate, evals, seeding) |
| 04 | [04-database-and-seed.md](04-database-and-seed.md) | Product columns, new tables, seed rules, Azure Search rules, **production rollout runbook (§7)** |
| 05 | [05-agent-guide.md](05-agent-guide.md) | How the agent works: loop, tools, guardrails, memory, evals, observability, cost, security |
| 06 | [06-phase-tracker.md](06-phase-tracker.md) | Phase status, all decisions D1–D9, phase log. **Keep it updated** |
| 07 | *(reserved)* `07-results.md` | Written in Phase 5 |
| 08 | [08-adr-why-build.md](08-adr-why-build.md) | ADR: why our own engine instead of Semantic Kernel / Agent Framework / Microsoft.Extensions.AI |
| 09 | [09-write-actions-design.md](09-write-actions-design.md) | Design only: "add to basket" with human approval |
| – | [reports/](reports/) | One HTML report per finished phase (`phase-N-report.html`) |

## 4. Goal (short)

1. Replace the brittle keyword + vector chat (`AiChatService`, `/api/ai/chat`) with a **tool-using agent**, based on SimpleAgent.
2. **More data:** about 1,000 products and policy documents.
3. **Make the flow visible:** chat with an observability panel, a runs list, an evals page, a "how it works" page.

## 5. Phases

| Phase | What | Status |
|---|---|---|
| 0 | Docs | ✅ Approved 2026-11-16 |
| 1 | Agent engine project (`Agent/`, generic, scoped, typed tools, guardrail checks, run store, eval runner with repeats + baseline gate, `gen_ai.*` telemetry names) | 🟨 Built + 38/38 tests pass; waiting for code review |
| 2 | Schema + seed (4 new Product columns, AgentRuns/EvalRuns/EvalCaseResults tables, ~1,000 products, policy files, idempotent seed) | ⬜ |
| 3 | Tools + API (7 read-only tools, `api/agent/*`, rate limit, system prompt file, ~45 eval cases) | ⬜ |
| 4 | Angular 11 UI (lazy `agent` module: chat + panel, runs, evals, how it works) | ⬜ |
| 5 | End to end + comparison with the old chat, then improvements: loop control (R11), shrinking tool results + multi-turn eval (R12), groundedness check, first baseline | ⬜ |
| 6 | Production rollout: cloud DB first, then the live index from the cloud DB, then App Insights traces | ⬜ |

Details for every step are in [02-implementation-plan.md](02-implementation-plan.md).

## 6. Decisions

### Made

| Topic | Decision |
|---|---|
| Engine location | New `Agent/` project in `eCom.sln`. **Never references Core, Infrastructure or API.** No NuGet packages |
| Tools location | `Infrastructure/AgentTools/`, wrapping existing services |
| Write actions | **Not built.** Read-only tools only. The design is in doc 09 |
| User identity in tools | Always from the JWT (`AgentRequestContext`), never from model arguments |
| Products | About 1,000; keep the 4 types (images exist only for Boards, Hats, Boots, Gloves) |
| D7 Azure Search | Cloud DB first, then fill the live index **from the cloud DB** (Phase 6). **No separate dev indexes**, so the laptop never syncs |
| D8 Images | **Reuse the 18 existing images.** Later option: 48 new ones (type × colour), made by hand in the Gemini/ChatGPT app (free) or via a paid image API (~$1–2) |
| D9 Product search by meaning | **Neither tags nor hybrid for now.** `search_products` stays SQL; known limitation in the BRD |
| Loop control (R11) | Copy the engine as is in Phase 1. Phase 5 adds `MaxToolCallsPerRun`, a configurable tool timeout, `IsError` steps and repeated-call detection. Tools run **sequentially**, never in parallel |
| Memory (R12) | In memory, last 20 messages per session, tied to the user. Phase 5 shrinks old tool results to stubs that keep the ids. No summarising, no persistence |
| Evals (R7) | ~45 cases with `expectedTools` + args; `repeat` 3 (5 for reports); a baseline regression gate run by hand before merging; groundedness check by code in Phase 5 |
| Telemetry | `gen_ai.*` names in Phase 1; App Insights exporter (in the API project) in Phase 6 |
| D4 Runs/evals pages | **Admin only** in production; open to all in dev (decided 2026-11-16). There is no Admin role yet: create it and give it to the owner's account in Phase 3/4 |
| D5 Test project | `Agent.Tests` (xUnit) **added in Phase 1** as recommended; owner to confirm in the review |
| D6 LLM API style | **Chat Completions kept** in Phase 1 as recommended; owner to confirm in the review |
| MCP | **Out** (owner skipped it) |
| Frameworks | Not used; reasons in doc 08 |

### Open (ask the owner; record the answers in 06-phase-tracker.md)

| # | Question |
|---|---|
| D1 | Product descriptions: LLM-written once (confirm cost first) or templates only? |
| D2 | Rate limit: 10 requests/minute per user or IP? |
| D3 | How long to keep agent runs? (recommended: 30 days) |

## 7. Facts about the repo (saves re-reading)

- **Stack:**
  - .NET 10 (`global.json` 10.0.301); controllers; Startup.cs + Program.cs; NLog.
  - EF Core + Npgsql, with two contexts: `StoreContext` (skinet) and `AppIdentityDbContext` (identity).
  - Repository + Specification pattern (`BaseSpecifcation<T>`, with the original spelling; the evaluator applies OrderBy *or* OrderByDescending).
  - **Redis is no longer used** (the basket is in the memory cache; `IBasketRepository` has Get/Update/Delete by basket id).
- **Startup switches in `Program.cs`:** `--migrate` / `RUN_MIGRATIONS`, `--init-search` / `INIT_SEARCH_INDEX`, `--sync-products` / `SYNC_PRODUCTS_TO_AZURE_SEARCH`, `--sync-rag` / `SYNC_RAG_TO_AZURE_SEARCH`. All `false` in production.
- **AI today:**
  - `AiChatService` does keyword intent plus a score gate.
  - `AiRetrieverService.SearchAsync(question, maxResults, sourceType, maxPrice)` runs a vector query on `rag-index`.
  - `AiDocumentService` builds documents from Products and DeliveryMethods.
  - OpenAI key and Azure Search are in `appsettings.Development.json`; the chat model setting is `OpenAI:ChatModel` (default `gpt-5.4-mini`, Responses API); embeddings use `text-embedding-3-small`.
  - Azure Search: `ecom.search.windows.net`, indexes `products-index` and `rag-index`, **the same in dev and prod config**.
- **SimpleAgent engine:**
  - `AgentRunner` loop: MaxSteps 5, token budget, history trim.
  - `ToolRegistry` has a 10 s timeout and returns errors to the model as text.
  - `GuardrailPipeline`: input runs length, injection and PII; output runs canary leak and PII.
  - `OpenAiLlmClient` uses **Chat Completions**.
  - Everything is singleton there; we need scoped.
- **Data (local):**
  - Docker Postgres `ecom-ai-m-db-1` (port 5432, user `appuser`), with 18 products, 6 brands, 4 types, 4 delivery methods, 0 orders, initial migration only.
  - Seed files: `Infrastructure/Data/SeedData/{products,brands,types,delivery}.json`.
  - `StoreContextSeed` only seeds empty tables today.
  - Images: `API/Content/images/products/*.png` (18 files; prefixes `sb-`, `hat-`, `boot-`, `glove-`).
  - `Order.BuyerEmail` holds the owner of an order.
  - Old eval cases: `docs/ai-retrieval-test-cases.csv` (21 cases).
- **Cloud:**
  - Azure Container Apps + Azure PostgreSQL `ecom-ai-pg-zahee01`, see `docs/container-app-github-deploy.md`.
  - Pushing to `main` triggers `.github/workflows/deploy-containerapp.yml`.
- **Client:**
  - Angular 11, Bootstrap 4, NgModules.
  - It needs **Node 16** (`nvm use 16.20.2`; the default Node 24 won't build it).
  - Dev server on port **4201**; API at `https://localhost:5001/api/`.
  - The existing AI page is `core/ai-playground` at `/ai-lab`.
- **Tools on the machine:**
  - Windows 11, PowerShell + Git Bash, Docker Desktop, Node (via nvm).
  - `dotnet ef` 8.0.28 is older than EF 10 and may warn.
  - **No Python.**

## 8. How to start a new session

1. Read this file (especially §2a), then [06-phase-tracker.md](06-phase-tracker.md) for the latest status and decisions.
2. Check git: `git -c safe.directory=* status` and `git -c safe.directory=* branch --show-current`. Expect `feature/agentic-ai`. Then run the two checks in §2a.
3. Do **only** what the owner asks for this session. If they say "start Phase N", follow that phase's steps in [02-implementation-plan.md](02-implementation-plan.md) and stop at its review checkpoint.
4. At the end of the session:
   - update "Last updated" (date **and** time), Status and Next action at the top;
   - add a line to the session log below;
   - update the phase tracker.

## 9. Session log

| Date / time | Assistant | What happened |
|---|---|---|
| 2026-11-15 (earlier) | Claude (planning, in `C:\Agents\SimpleAgent`) | Goal and phases agreed. A first draft of agent code was written and then **reverted** at the owner's request. DB backup taken. |
| 2026-11-15 04:56 EST | Claude Code | **Phase 0 docs written** (01–06, 08, 09). Review rounds added: R11 loop control, R12 multi-turn memory, stronger evals (R7, S1–S9), production rollout runbook (Phase 6), ADR, write-actions design, `gen_ai.*` telemetry + App Insights. Decisions D7, D8 and D9 made; MCP skipped. No code, config, DB or git changes. |
| 2026-11-16 21:02 EST | Claude Code | D4 decided (admin only). **Phase 1 built** on new branch `feature/agentic-ai`: `Agent/` engine + `Agent.Tests/` (38/38 pass), both added to `eCom.sln`. No API, Infrastructure, DB, config or Dockerfile changes; nothing committed. API start not checked (Docker Desktop was off). Docs 02, 04, 05, 06 updated; HTML phase report added and made a rule (#10). |
| 2026-11-16 23:21 EST | Claude Code | Handoff refreshed for the next session: added §2a "Resume here" (uncommitted files, how to check, loose ends, what to do on review or "start Phase 2"). No code changes. |
