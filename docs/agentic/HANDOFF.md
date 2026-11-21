# HANDOFF: AI agent for eCOM-AI-M

> **Last updated:** 2026-11-21 01:15 EST
> **Status:** Phases 1 and 2 approved, committed and pushed on `feature/agentic-ai`. The deploy pipeline is **fixed and working** (PR #1 merged to `main`, run `44873563741` green; live site serves the 18 original products). Shop UI tidied for 1,018 products.
> **Next action:** wait for the owner to say "start Phase 3", then follow **§2b (Phase 3 brief)**. Do **not** merge `feature/agentic-ai` into `main`: Phase 2 needs DB columns the cloud DB doesn't have yet (Phase 6).

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
| DB backups (before Phase 2) | `C:\Backups\skinet-before-phase2.sql`, `C:\Backups\identity-before-phase2.sql` (permanent folder) |

## 2a. Resume here (state at the end of the last session)

**Committed:** Phase 1 (`Agent/`, `Agent.Tests/`, `eCom.sln`, `docs/agentic/`) as `4ab0817`. Not pushed.

**Uncommitted Phase 2 work** (ours; commit only when asked):

| Path | What |
|---|---|
| `Core/Entities/Product.cs`, `API/Dtos/ProductToReturnDto.cs` | 4 new fields: Color, Material, StockQuantity, Rating |
| `Infrastructure/Infrastructure.csproj` | Now references `Agent` |
| `Infrastructure/Data/StoreContext.cs`, `Data/Config/{ProductConfiguration,JsonColumn,AgentRunConfiguration,EvalRunConfiguration}.cs` | New tables + mappings |
| `Infrastructure/Data/Migrations/20261120044648_AgentAndProductAttributes*` + snapshot | The migration (already applied to the local DB) |
| `Infrastructure/Data/StoreContextSeed.cs` | Idempotent seed |
| `Infrastructure/Data/SeedData/{products,brands}.json`, `SeedData/policies/*.md` | Generated catalogue + 6 policy files |
| `Infrastructure/Services/{PolicyDocumentLoader,AiDocumentService}.cs` | Policy documents for the AI |
| `tools/seed-gen/` | Generator + copies of the original products/brands |
| `docs/agentic/` | Tracker, this file, `reports/phase-2-report.html` |

Everything else in `git status` (`API/Startup.cs`, `Dockerfile`, `API/logs/`, `CLAUDE.md`, `tmp/`, `docs/container-app-github-deploy.md`) is the **owner's**. Leave it alone (rule 4).

**Check it still works** (Docker Desktop on; free, no network):

```powershell
dotnet build eCom.sln            # expect 0 errors; 5 old warnings in API code only
dotnet test Agent.Tests          # expect Passed: 38, Failed: 0
node tools/seed-gen/generate.js  # rewrites the same files: git shows no change
cd API; dotnet run -- --migrate  # "Seed finished: added 0 brands, 0 types, 0 products"
```

**Local DB state:** `skinet` has 1,018 products, 20 brands, both migrations. Backups from before Phase 2: `C:\Backups\skinet-before-phase2.sql`, `C:\Backups\identity-before-phase2.sql`.

**Loose ends:**
- **Dockerfile:** the two `Agent` COPY lines were added with the owner's OK (2026-11-20). The rest of the file is still the owner's uncommitted edits: when committing, ask whether to include the Dockerfile (it mixes their changes and ours).
- Policy text: the owner said OK (2026-11-20). `support@skinet-shop.example` is still a placeholder.
- Phase 3 extras (doc 02): `Program.cs` `--evals`; the new tool signature; a DB-backed `IAgentRunStore`/`IEvalRunStore` using the new tables.
- There is no Admin role in identity yet. D4 needs one in Phase 3/4.
- `appsettings.json` is gitignored. Put new config examples in the docs, not only in that file.
- PowerShell 5.1: `git commit -F -` with a here-string doesn't work; write the message to a file in the scratchpad and use `-F <file>`.

**If the owner asks for review changes:** edit, re-run build/tests (and the seed if data changed), update the Phase 2 report and tracker. Stop again for review.

**When the owner says "start Phase 3":** follow §2b below, then Phase 3 in doc 02. End with `reports/phase-3-report.html` (rule 10).

## 2a-bis. Deploy / Dockerfile findings (2026-11-20) — ✅ all fixed 2026-11-21

Done with the owner on 2026-11-21:
- New service principal `github-ecom-ai-deploy` (Contributor on resource group **`eCom`**, which holds ACR `ecomaiacrzahee01` and Container App `ecom-ai-ca-zahee01`). `AZURE_CREDENTIALS` (JSON), `RESOURCE_GROUP`, `ACR_NAME`, `CONTAINER_APP_NAME` secrets reset.
- Old leaked principal `github-ecom-containerapp-deploy` **deleted** (and its role assignment); failed run `27458172472` deleted.
- `global.json` got `"rollForward": "latestFeature"` (the `sdk:10.0` image is 10.0.404). Dockerfile (.NET 10) + `global.json` merged to `main` via PR #1; deploy run `44873563741` succeeded. Live: `https://ecom-ai-ca-zahee01.grayflower-719cfccf.eastus.azurecontainerapps.io`.
- Still open (low priority): bump `actions/checkout@v4` / `azure/login@v2` (Node 20, forced onto Node 24 with a warning). Side note for the owner: `screentrack-github-deploy` has Contributor + RBAC Admin on the whole subscription.

Original findings, kept for history:

| # | Finding | Status / fix |
|---|---|---|
| 1 | **Committed `Dockerfile` (`b0c3ebe`, also on `main`) can't build or run the app.** It uses `dotnet/sdk:7.0` and `dotnet/aspnet:5.0`, but the app targets .NET 10 | The owner's **uncommitted** edits already fix it (`sdk:10.0`, `aspnet:10.0`, `npm ci`), plus our 2 `Agent` COPY lines. **Commit the Dockerfile** (recommended: with Phase 2) |
| 2 | **GitHub deploy fails at "Log in to Azure"** (only run: `27458172472`, 2026-06-13). The Docker build was never reached. `AZURE_CREDENTIALS` holds a bare client secret, but `azure/login@v2` needs JSON: `{ "clientId", "clientSecret", "subscriptionId", "tenantId" }` (e.g. `az ad sp create-for-rbac ... --json-auth`) | **Owner's job** (GitHub + Azure). Claude can't and shouldn't change secrets |
| 3 | ⚠️ **The failed run's error message shows the first characters of that client secret.** Anyone who can see the repo's Actions page can read them | **Owner: rotate the client secret** in Azure when creating the JSON credential |
| 4 | Possible: `global.json` pins SDK `10.0.301` with no `rollForward`. If the `sdk:10.0` image has a different feature band, `dotnet restore` fails in Docker | Not verified. Fix if needed: add `"rollForward": "latestFeature"`. Check with a local `docker build .` (free, a few minutes) **after the owner OKs it** |
| 5 | GitHub warns that `actions/checkout@v4` / `azure/login@v2` run on Node 20, which has been removed from runners since 2026-09-16 | Low priority: bump the action versions when touching the workflow |

Fine as is: `client/package-lock.json` is committed (so `npm ci` works); `.dockerignore` excludes `bin`, `obj`, `node_modules`.

**Next session, first ask the owner:** (a) did they fix and rotate `AZURE_CREDENTIALS`? (b) run `docker build .` locally now? (c) commit Phase 2 together with the Dockerfile?

## 2b. Phase 3 brief: tools and API (next phase)

**Goal:** wire the Phase 1 engine into the API with the shop's 7 read-only tools, so you can chat with the agent through Swagger. No UI yet (Phase 4).

**Before writing code:**
1. Phase 2 is committed and pushed (2026-11-21), so Phase 3 starts clean.
2. Start Docker Desktop (`& "C:\Program Files\Docker\Docker\Docker Desktop.exe"`, then wait until `docker info` works). Run the checks in §2a.

**Steps** (full detail in [02 Phase 3](02-implementation-plan.md#phase-3--tools-and-api), tool contracts in [05 §3](05-agent-guide.md#3-tools)):

| Step | Work | Notes for this repo |
|---|---|---|
| 3.1 | 7 tools in `Infrastructure/AgentTools/`: `search_products`, `get_product_details`, `list_catalog_filters`, `search_knowledge`, `get_delivery_options`, `get_my_orders`* , `get_order`* (*sign-in, `IRequiresSignIn`) | Signature: `ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken)`. User email **only** from `context.UserEmail`, never from arguments. Orders are owned by `Order.BuyerEmail` |
| 3.2 | `ProductSearchSpecification`: brand, type, colour, material, price range, in stock, min rating; sort price/rating/name | Spec base class is spelled `BaseSpecifcation<T>`; the evaluator applies OrderBy **or** OrderByDescending. Unknown brand/type → return the list of valid names |
| 3.3 | `search_knowledge`: Azure retriever if configured, else local keyword search over the policy files | Reuse `PolicyDocumentLoader.Load()` (Phase 2). Result says `source: azure` or `local`. **Never sync to Azure** (rule 8) |
| 3.4 | `EfAgentRunStore`, `EfEvalRunStore` | Tables already exist (Phase 2). Also add **30-day retention** (D3): delete `AgentRuns` older than 30 days, e.g. a cleanup on startup or a hosted service |
| 3.5 | `AgentController`: `POST api/agent/chat`, `GET runs`, `runs/{id}`, `stats`, `info`, `POST evals`, `GET evals`, `evals/{id}` | Runs/evals endpoints: admin only in production, open in dev (D4). No Admin role exists yet: create it and give it to the owner's account |
| 3.6 | Fill `AgentRequestContext` from the JWT | Chat allows anonymous **and** signed-in |
| 3.7 | Rate limit, built-in `Microsoft.AspNetCore.RateLimiting` | **D2: per user**, 10/minute; anonymous falls back to IP |
| 3.8 | `API/Agent/system-prompt.md` + `Agent` section in `appsettings.json` | `appsettings.json` is gitignored: also put the config example in the docs. Look up current OpenAI prices for `Pricing`. Read the key from `OpenAI:ApiKey` |
| 3.9 | `API/Agent/evals/cases.json`, ~45 cases with `expectedTools` | Port the 21 in `docs/ai-retrieval-test-cases.csv`. TC-012…021 are answered by the Phase 2 policy files. Order cases: the seeded user is `bob@test.com`, but there are **0 orders** locally, so create a test order for him (ask first if it means seeding data) |
| 3.10 | Swagger smoke test of every endpoint, then **review checkpoint** | Real OpenAI calls cost a little: keep testing small; full eval runs only with the owner's OK (rule 7) |

**Also in Phase 3:**
- `Program.cs`: `EvalCommandLine.IsEvalCommand(args)` / `RunAsync(...)`, so `dotnet run --evals` works.
- Register in `Startup.cs` with `services.AddAgent(Configuration).AddTool<...>()`. **`Startup.cs` has the owner's uncommitted edits:** add only our lines, and tell the owner.
- Swap the in-memory run/eval stores for the EF ones.

**Done when:** `POST api/agent/chat` answers a product question using `search_products`, a policy question using `search_knowledge`, refuses "my orders" when anonymous and answers it for `bob@test.com`; runs appear in `GET api/agent/runs`; a small eval run completes; build and tests pass.

**Practical tips from Phase 2:**
- Run the API in the background, wait for "Application started" in the log, and stop it with `Stop-Process` on `API.exe`. The "exit code 255" afterwards is expected.
- The default launch profile redirects to `https://localhost:5001`. Windows PowerShell doesn't trust the dev certificate, so use `curl.exe -sk` instead of `Invoke-RestMethod`.
- `dotnet ef` is 8.0.28. It works for EF 10 with only a warning.

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
| 1 | Agent engine project (`Agent/`, generic, scoped, typed tools, guardrail checks, run store, eval runner with repeats + baseline gate, `gen_ai.*` telemetry names) | ✅ Approved, committed `4ab0817` (2026-11-19) |
| 2 | Schema + seed (4 new Product columns, AgentRuns/EvalRuns/EvalCaseResults tables, ~1,000 products, policy files, idempotent seed) | ✅ Approved, committed + pushed (2026-11-21) |
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
| D5 Test project | `Agent.Tests` (xUnit) kept (confirmed 2026-11-19) |
| D6 LLM API style | Chat Completions kept (confirmed 2026-11-19) |
| D1 Descriptions | Templates for now (2026-11-19). LLM-written stays an option after a cost check |
| MCP | **Out** (owner skipped it) |
| Frameworks | Not used; reasons in doc 08 |

| D2 Rate limit | **Per user**, 10 requests/minute (2026-11-20); anonymous visitors fall back to IP |
| D3 Run retention | **30 days** (2026-11-20) |

### Open

None. All decisions D1–D9 are made.

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
  - Docker Postgres `ecom-ai-m-db-1` (port 5432, user `appuser`). Since Phase 2: 1,018 products, 20 brands, 4 types, 4 delivery methods, 0 orders; migrations `PostGres initial` + `AgentAndProductAttributes`. Other Postgres containers (`ecom-ai-db-1`, `skinet-v6-main-db-1`) start with Docker but aren't on port 5432; ignore them.
  - Seed files: `Infrastructure/Data/SeedData/{products,brands,types,delivery}.json` + `policies/*.md`. `products.json`/`brands.json` are generated by `tools/seed-gen/generate.js`.
  - `StoreContextSeed` is idempotent (adds missing rows by name).
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
| 2026-11-19 23:58 EST | Claude Code | Owner approved Phase 1 → committed `4ab0817` (D5, D6 confirmed; D1 = templates for now). **Phase 2 built**: backups in `C:\Backups`, 4 Product columns, AgentRuns/EvalRuns/EvalCaseResults, migration `AgentAndProductAttributes`, `tools/seed-gen/generate.js` (1,018 products, 20 brands), 6 policy files, idempotent seed, `PolicyDocumentLoader`. Local DB migrated + seeded and checked (originals unchanged, second run adds 0, API starts). Dockerfile needs 2 `Agent` COPY lines: asked the owner. Phase 2 not committed. |
| 2026-11-20 | Claude Code | Owner OK'd the policy text and the Dockerfile lines (added). D2 = per user, D3 = 30 days: all decisions made. Added §2b, the Phase 3 brief, for the next session. Phase 2 still not committed. Then checked the Dockerfile and deploy: the committed Dockerfile uses .NET 7/5 images (the owner's uncommitted edits fix that), the only deploy failed at Azure login (bad `AZURE_CREDENTIALS` format; part of the secret shows in the run log, so rotate it), and the `global.json` pin is unverified. Logged in §2a-bis; nothing changed. |
| 2026-11-21 01:15 EST | Claude Code | **Deploy fixed** with the owner: new service principal + GitHub secrets, old leaked principal and run log deleted, `global.json` rollForward, PR #1 (Dockerfile + global.json) merged to `main`, first green deploy. Shop UI tidied (pager max 5 pages, compact filter panel with scrolling brand list, 72px header, full-width layout, 4 per row, 12 per page). Phase 2 + UI committed and pushed on `feature/agentic-ai`. Note: local Angular dev server ran on **https://localhost:4200** (not 4201). |
