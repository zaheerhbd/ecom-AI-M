# HANDOFF: AI agent for eCOM-AI-M

> **Last updated:** 2026-11-26 00:30 EST
> **Status:** **Phase 7 built, waiting for code review (not committed).** Microsoft Agent Framework is a second engine behind `Agent:Engine` (`Custom` default / `AgentFramework`); API, UI and DB unchanged; tests 73/73; live Gemini check OK. Report: [reports/phase-7-report.html](reports/phase-7-report.html). **Phase 6 is still open:** admin role ✅ and index refill ✅ (2026-11-23 20:00 log line), but `dcb17bb` + `1486d98` are not merged/deployed yet, and 6.6 smoke test + Phase 6 report are not done.
> **Next action:** owner reviews Phase 7 → "commit phase 7". Then finish Phase 6: PR `feature/agentic-ai` → `main` (outward-facing, owner's OK), 6.6 smoke test, Phase 6 report. Optional: engine comparison run (7.8, ≈ $0.80–1.50 on OpenAI, needs OK).

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

## 2a-0. Resume here: Phase 6 in progress (2026-11-23)

**Where we are.** `main` = `675f946` (PR #3). Phases 1–5 are live. Branch `feature/agentic-ai` = `f1afda0` (same content as `main`). Live app: `https://ecom-ai-ca-zahee01.grayflower-719cfccf.eastus.azurecontainerapps.io`. Latest revision `--0000025`, healthy, `RUN_MIGRATIONS=false`, `maxReplicas = 1`.

| Step | Status | Details |
|---|---|---|
| Container App settings | ✅ | `Gemini__ApiKey` → secret `gemini-api-key`; `Agent__Llm__Provider=Gemini`; `Gemini__Model=gemini-3.5-flash`; `Agent__AdminEmails__0` = the owner's email (**never write it into git**: a commit was blocked once for this) |
| 6.1 Backup | ✅ | Azure restore point **2026-11-23T05:23:23Z**; `C:\Backups\skinet-before-phase6.sql`, `C:\Backups\identity-before-phase6.sql` (complete) |
| 6.2 Deploy | ✅ | The owner merged **PR #2** (05:32 UTC, Phases 1–5) and **PR #3** (05:38 UTC, adds the 6.1 notes). Both deploys succeeded |
| 6.3 Migrate + seed | ✅ | `RUN_MIGRATIONS=true` was set before the merge, so PR #2's first start (revision 23) migrated + seeded; revision 24 then found nothing to do. Switched back to `false` |
| **Admin role** | ⏳ **do first** | Role `Admin` exists, 0 members. The seed only adds the role to accounts that exist, and the owner registered **after** it ran. Fix: `az containerapp update ... --set-env-vars RUN_MIGRATIONS=true` → wait until healthy → check the log for no errors → set `false`. Safe: migrations are up to date and the seed is idempotent. Then check `AspNetUserRoles` = 1 |
| 6.4 Index schema | ⏳ | First record the doc counts of `products-index` and `rag-index` (runbook step 2, not done yet; read-only, ask first). Then `INIT_SEARCH_INDEX=true` once → `false` |
| 6.5 Refill indexes | ⏳ | `SYNC_PRODUCTS_TO_AZURE_SEARCH` + `SYNC_RAG_TO_AZURE_SEARCH` once → `false`. OpenAI embeddings for ~1,000 products: **under $0.01; tell the owner before running** |
| 6.6 Smoke test | ⏳ | Shop list/paging, product page, basket, old `/ai-lab`, `/agent` chat (product, policy, signed-in orders), Runs/Evals as admin, 401/403 for others. Gemini free = 20 requests/day, so keep it short. Record in `07-results.md` |
| 6.7 App Insights | optional | Ask the owner |
| Report | ⏳ | `reports/phase-6-report.html` + tracker row |

**Live DB after 6.3** (checked 2026-11-23): `skinet` Products 1,018 (ids 1–18 still there), Brands 20, Types 4, DeliveryMethods 4, Orders 0, AgentRuns 0, EvalRuns 0; migrations `PostGres initial`, `AgentAndProductAttributes`, `AgentGroundedness`. `identity` Users 4 (+ the owner's new account), Roles 1 (`Admin`), UserRoles 0.

**How to reach the cloud DB (read-only checks):**
- Add a firewall rule for the laptop IP: `az postgres flexible-server firewall-rule create -g eCom -n ecom-ai-pg-zahee01 -r <name> --start-ip-address <ip> --end-ip-address <ip>`. **Delete it afterwards.**
- Read `ConnectionStrings__DefaultConnection` from the Container App into a shell variable, never printing it. Its keys are `Host`, `Port`, `Database`, `Username`, `Password`, `Ssl Mode`.
- Run `psql`/`pg_dump` through the local container: `docker exec -e PGPASSWORD ecom-ai-m-db-1 psql "host=... dbname=... user=... sslmode=require"`.

**Loose ends found in Phase 6:**
- The other Container App settings (connection strings, `Token__Key`, `OpenAI__ApiKey`, `AzureSearch__ApiKey`) are plain values, not secrets. Move them later.
- The start-up log shows `libgssapi_krb5.so.2: cannot open shared object file` (the Postgres driver looks for Kerberos; harmless) and `Failed to determine the https port` (harmless behind the Container App proxy).
- Gemini free tier = 20 requests/day. Fine for a demo, not for real customers.
- `az containerapp logs show --tail` allows at most 300 lines, and an old revision's logs are gone once it is replaced.

## 2a. Earlier state (end of the 2026-11-22 session, kept for reference)

**Committed and pushed** on `feature/agentic-ai`: Phase 1 (`4ab0817`), Phase 2 (`a9679aa`), shop layout tidy (`eafe5c9`), Phase 3 (`c5ca42f`), Phase 4 + architecture report (`47cb92a`). `main` has only the deploy fix (PR #1). **Never merge `feature/agentic-ai` into `main` before Phase 6** (the cloud DB lacks the Phase 2 and Phase 5 columns).

**Phase 5 is built and unit-tested but NOT committed** (2026-11-22, waiting for the owner's code review). Results and what is still unmeasured: [07-results.md](07-results.md). Report: [reports/phase-5-report.html](reports/phase-5-report.html). Files:

| Path | What |
|---|---|
| `Agent/AgentOptions.cs` | `MaxToolCallsPerRun` (10), `ToolTimeoutSeconds` (10), `ShrinkOldToolResults` (true) |
| `Agent/AgentRunner.cs` | Per-answer `TurnState`: identical calls (`ToolCallKey` = name + sorted, lower-cased args) are not run again (`IsRepeat` step, model told to reuse the result); cap → stop reason `max-tool-calls` (refused calls still get a tool result so history stays valid); `CheckGroundedness` (evidence = history minus system prompt and the answer itself); `ShrinkToolResults` before saving memory |
| `Agent/AgentResult.cs`, `Agent/Runs/AgentRunRecord.cs` | `AgentStep.IsRepeat`, `AgentStopReasons.MaxToolCalls`, `GroundedFactCount`, `MadeUpFacts` |
| `Agent/Tools/ToolResultSummariser.cs` (new), `ToolRegistry.cs` | Default stub `[tool: N items, ids …]` or first 200 chars; optional `ISummarisingTool`; registry `Find` + `Timeout`; timeout from config (`ServiceCollectionExtensions`) |
| `Agent/Evals/Groundedness.cs` (new), `Graders.cs`, `EvalCase.cs`, `EvalCaseLoader.cs`, `EvalRunner.cs`, `EvalRecords.cs`, `EvalCommandLine.cs` | Regex groundedness; `turns` (multi-turn, one scope per message, last answer graded, input tokens per turn logged) and `maxToolCalls`; eval run `FactsChecked` / `MadeUpFacts` / `MadeUpRate` |
| `Infrastructure/Data/Config/AgentRunConfiguration.cs`, migration `20261122220351_AgentGroundedness` + snapshot | New columns (jsonb default fixed to `[]`). **Applied to the local DB only**; the cloud DB gets it in Phase 6.3 |
| `API/Agent/evals/cases.json` | 51 → 56 cases: `OR-004`, `MM-001`, `MM-002`, `LC-001`, `LC-002` |
| `client/src/app/agent/` (models, timeline html/ts/scss, evals html) | Amber repeats, groundedness note, "too many tool calls" text, made-up-rate card |
| `Agent.Tests/LoopControlAndMemoryTests.cs` (new, 14 tests), `TestHost.cs` (extra test tools), `AgentRunnerTests.cs` (MaxSteps test uses different args) | 57/57 |
| `docs/agentic/07-results.md` (new), `docs/agentic/dev/seed-test-order.sql` (new), `reports/phase-5-report.html` (new), tracker, this file | Docs + local-only test order for bob |

**Phase 5 tests (2026-11-22):** `dotnet build eCom.sln` 0 errors (5 old warnings); `dotnet test Agent.Tests` 57/57; `ng build` 0 errors. Fake-model API check: new JSON fields come back and are saved. Shrinking on vs off (Fake model, 3-message chat): input tokens 1,548 / 928 / 970 vs 1,548 / 1,110 / 1,152 (−182 per later message). Live Gemini run of the 5 new cases: **HTTP 429**, free daily quota (20 requests/day per model, `GenerateRequestsPerDayPerProjectPerModel-FreeTier`) used up; agent answered politely and saved `LlmException`. Timeline/evals UI changes not seen with live answers.

**Local data change:** bob@test.com now has order 1 (Angular Speedster Board 2000 + Green React Woolen Hat, UPS2 "2-5 Days", subtotal $208) from `docs/agentic/dev/seed-test-order.sql` (idempotent, **local only**).

**Open questions for the owner (ask next session):**
1. Code review of Phase 5 → "commit phase 5"? Push?
2. Run the official baseline before going live? Recommended: OpenAI ×5 ≈ $0.75 (`dotnet run -- --evals --repeat 5 --save-baseline` with `Agent:Llm:Provider = OpenAI`). Free Gemini can't do it (20 requests/day).
3. Which LLM in production? Free Gemini isn't enough for a live shop (20 requests/day; Google may use the data). Recommended: OpenAI (`Agent__Llm__Provider=OpenAI` + key as a Container App secret) or a paid Gemini key.
4. The owner's email for `Agent:AdminEmails` in production (so Runs/Evals pages open there).

**Phase 4 (committed `47cb92a`)** files, for reference:

| Path | What |
|---|---|
| `client/src/app/agent/` (new) | Lazy `AgentModule` at `/agent` (tabs: `chat`, `runs`, `runs/:id`, `evals`, `how-it-works`). `agent.service.ts` + `agent.models.ts` (API calls, JSON shapes; `GuardrailOutcome` is a number enum 0–3), `agent-pipes.ts` (`agentMarkdown`: escapes first, then bold/lists; `prettyJson`), `agent-access.ts` (401/403 → "log in" / "admins only"), `agent-shell/` (tab bar), `agent-timeline/` (shared "how this answer was made" panel: totals, guardrail chips, expandable steps, sources), `agent-chat/` (sessionId + messages in `sessionStorage`, suggestions, 429 notice), `agent-runs/` + `agent-run-detail/`, `agent-evals/` (quick check PS-001/TC-018/SF-001 by default; category / case ids / all-with-confirm; polls every 3 s), `agent-how-it-works/` (live `api/agent/info` panel + loop SVG) |
| `client/src/app/app-routing.module.ts` | `agent` lazy route, breadcrumb "Assistant" |
| `client/src/app/core/nav-bar/nav-bar.component.html` | "Assistant" link |
| `client/src/app/core/interceptors/loading.interceptor.ts` | Skips the full-screen spinner for `/agent/` calls |
| `docs/agentic/reports/phase-4-report.html` | Phase 4 report (screenshots embedded) |
| Also uncommitted, from earlier on 2026-11-22 | `docs/agentic/reports/agent-architecture.html` (solution design report, owner asked for it), HANDOFF/tracker "pushed" notes |

**Phase 4 tests (2026-11-22):** `ng build` and `ng build --prod`: 0 errors, 0 warnings. Headless Chrome driven by a CDP script (scratchpad, not in repo): anonymous chat "red gloves under $50" → `search_products`, 11 found, timeline OK; follow-up "which is cheapest?" remembered ($12 Alpine Forge); tool step expands; runs list + detail; evals quick check started from the UI → 3/3 pass with progress bar; how-it-works live info; signed in as bob → `get_my_orders` "no orders yet"; **no console errors**. Not tested in the browser: the 429 notice and the production admin-only messages (API side tested in Phase 3); `ng test` not run (no specs added).

**Phase 3 is committed** on `feature/agentic-ai` (`c5ca42f`, 2026-11-22, owner said "commit phase 3"; pushed). Files:

| Path | What |
|---|---|
| `Infrastructure/AgentTools/*` (new) | 7 tools + `ToolHelpers` + `LocalPolicySearch` (keyword search over the policy files) |
| `Core/Specifications/ProductSearchParams.cs`, `ProductSearchSpecification.cs` (new) | Filters/sorting for `search_products` |
| `Infrastructure/Data/EfAgentRunStore.cs`, `EfEvalRunStore.cs` (new) | DB stores for runs/evals |
| `Infrastructure/Services/AgentRunCleanupService.cs` (new) | Deletes runs older than `Agent:RunRetentionDays` (30), daily |
| `API/Controllers/AgentController.cs`, `API/Dtos/AgentDtos.cs`, `API/Extensions/AgentServiceExtensions.cs` (new) | Endpoints, DI wiring, rate limit, admin policy, shop defaults (prompt path, pricing) |
| `API/Agent/system-prompt.md`, `API/Agent/evals/cases.json` (new) | Prompt + 51 eval cases; published with the app (`API.csproj`) |
| `API/Startup.cs` | **2 lines only** (`AddShopAgent`, `UseRateLimiter`); the owner's earlier edits were already committed |
| `Agent/Llm/*`, `Agent/ServiceCollectionExtensions.cs`, `Agent.Tests/ProviderSettingsTests.cs` | **Gemini provider (2026-11-22)**: `Agent:Llm:Provider` = `Gemini` (default in config) or `OpenAI`. Each provider keeps its own section (`Gemini:ApiKey/Model`, `OpenAI:ApiKey/ChatModel`), resolved by `ApplyProviderDefaults`. Gemini uses its OpenAI-compatible endpoint; the client sends back Gemini's `extra_content` (thought signature) on tool calls, honours `Retry-After` (≤60s). Price default $0 on Gemini |
| `API/Program.cs` | `--evals` switch; Admin role seeding on `--migrate` |
| `API/Extensions/IdentityServiceExtensions.cs`, `Core/Interfaces/ITokenService.cs`, `Infrastructure/Services/TokenService.cs`, `API/Controllers/AccountController.cs`, `Infrastructure/Identity/AppIdentityDbContextSeed.cs` | Admin role (D4): roles on, role claims in the JWT, `SeedAdminRoleAsync` for `Agent:AdminEmails` |
| `API/API.csproj`, `API/appsettings.Production.json.example` | Ship prompt/cases; `Agent` config example |
| `docs/agentic/` | Tracker, this file, `reports/phase-3-report.html` |
| *(gitignored, local only)* `API/appsettings.Development.json` | `Agent` section (Provider **Gemini**, rate limit, retention, `UseAzureSearch: false`, `AdminEmails: []`), `Gemini` section (owner's free key, model `gemini-3.5-flash`), `OpenAI:ChatModel`. Logging `Microsoft` = Warning (owner). Backups: `%TEMP%\appsettings.Development.backup.json`, `%TEMP%\appsettings.Development.before-gemini.json` |

Owner's files, still leave alone (rule 4): `API/Startup.cs` (except our 2 lines), `API/logs/`, `CLAUDE.md`, `tmp/`, `docs/container-app-github-deploy.md`.

**Check it still works** (Docker Desktop on):

```powershell
dotnet build eCom.sln            # 0 errors; 5 old warnings in API code
dotnet test Agent.Tests          # Passed: 43
cd API; dotnet run               # then Swagger: POST /api/agent/chat { "message": "Show me navy hats under $30" }
dotnet run -- --evals --repeat 1 --cases <small file>   # CLI eval; free on Gemini (daily cap), ~$0.40 for 51x3 on OpenAI
```

**Gemini test (2026-11-22):** red gloves → `search_products` (11 found), returns after 5 weeks → `search_knowledge` (no, 30 days), delivery → `get_delivery_options`; all $0, 2–25 s. `gemini-2.5-flash` is retired for new keys (404); `gemini-3.8-flash`, `3.7-flash`, `flash-latest` returned 503 "high demand" that day, so the model is `gemini-3.5-flash`. List models: `GET https://generativelanguage.googleapis.com/v1beta/openai/models` with the key as Bearer. Free tier: Google may use the data, so production should use the paid tier or OpenAI.

**Test results (2026-11-21):** product search (right filters), policy answer (local search), anonymous vs bob orders, foreign order → "not found", injection blocked ($0), 11th msg/min → 429, runs/stats saved, Production mode → runs 401 anonymous / 403 non-admin, eval via API (2 cases) and CLI (3 cases, 3/3 pass), prompt + cases included in `dotnet publish`. Two prompt problems found by testing and fixed (see Phase 3 report).

**Loose ends:**
- **Gemini free tier = 20 requests/day per model** (found 2026-11-22). One question = 2–3 requests, so ~7–10 questions a day. Fine for trying the chat; not for evals or production.
- Phase 5 measured runs still to do: S1–S9, made-up rate on real answers, baseline file, real-model token saving (`MM-002` with `ShrinkOldToolResults` on/off). See 07-results.md §4.
- Old-chat comparison (5.3/5.4) skipped by the owner; `AiChatService` stays, no retire decision.
- `support@skinet-shop.example` in the policies is still a placeholder.
- Rate limit is per IP for anonymous users; behind Azure Container Apps that is the proxy IP until forwarded headers are configured (Phase 6).
- `Agent:AdminEmails` is empty: the owner adds their email in production (Phase 6), then runs with `--migrate`/`RUN_MIGRATIONS`.
- Production needs `Agent__Llm__Provider` (`Gemini` + `Gemini__ApiKey`, or `OpenAI`), else the engine's free Fake model answers. See `appsettings.Production.json.example`.
- Gemini's `usage.completion_tokens` leaves out thinking tokens, so token counts look low; cost is $0 on the free tier anyway.
- No unit tests for the shop tools yet (Agent.Tests covers the engine only).
- Gemini free tier is slow at times: 2–60 s per answer on 2026-11-22 (one LLM call took 32 s). The UI copes ("thinking…"); OpenAI is faster for demos.
- An eval answer from 2026-11-21 (OR-001, OpenAI) had its text repeated twice. Not seen since; not re-checked in Phase 5 (no live runs).
- Runs totals include eval runs (marked "test" in the table). A channel filter could come later.
- Two dev servers on 4200: if `ng serve` asks "Port 4200 is already in use", an earlier one is still running and already serves the new code (live reload).
- `appsettings.json` is gitignored: config examples live in `appsettings.Production.json.example` and the docs.
- PowerShell 5.1: `git commit -F -` with a here-string doesn't work; use Git Bash heredoc or `-F <file>`.

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

## 2b. Phase 6 brief: production rollout (next phase, only when the owner says "start Phase 6")

**Goal:** put Phases 1–5 on the live site. Load the new data into the **cloud database first**, then fill the **live index from the cloud database**. Full runbook with checks and rollback: [04 §7](04-database-and-seed.md#7-production-rollout-cloud-database-then-index); steps in [02 Phase 6](02-implementation-plan.md#phase-6--production-rollout).

**Every step is outward-facing (Azure, GitHub, the live DB).** Do one step at a time, say exactly what it will do, and wait for the owner's OK each time. Live app: `https://ecom-ai-ca-zahee01.grayflower-719cfccf.eastus.azurecontainerapps.io` (resource group `eCom`, ACR `ecomaiacrzahee01`, Container App `ecom-ai-ca-zahee01`, Postgres `ecom-ai-pg-zahee01`).

**Before step 6.1, settle with the owner:**
1. Phase 5 committed + pushed (open question 1).
2. Production LLM (open question 3): `Agent__Llm__Provider` + its key as a **Container App secret** (never in git). Without it, production answers with the free Fake model.
3. `Agent__AdminEmails__0` = the owner's email (open question 4), so `--migrate` gives them the Admin role.
4. Optional: the baseline (open question 2) as the "before" picture.

| Step | Work | Notes for this repo |
|---|---|---|
| 6.1 | Back up the cloud `skinet` DB: note a point-in-time restore time + `pg_dump` to `C:\Backups\` (permanent). Record row counts (Products, ProductBrands, Orders, DeliveryMethods) | Needs the owner's firewall/credentials for `ecom-ai-pg-zahee01`. Read-only |
| 6.2 | Owner merges `feature/agentic-ai` → `main` (PR). The workflow deploys the image | The workflow's `actions/checkout@v4` / `azure/login@v2` Node-20 warning is harmless; bump later |
| 6.3 | Migrate + seed once: `RUN_MIGRATIONS=true`, **1 replica**, then back to `false`. Applies `AgentAndProductAttributes` **and `AgentGroundedness`**, seeds ~1,000 products, policies, the Admin role. Verify counts | Seed is idempotent. **Never** run `docs/agentic/dev/seed-test-order.sql` there |
| 6.4 | `INIT_SEARCH_INDEX=true` once (index schema for the new product fields), then `false` | |
| 6.5 | `SYNC_PRODUCTS_TO_AZURE_SEARCH` / `SYNC_RAG_TO_AZURE_SEARCH` once each from the **cloud** DB, then `false` | Uses OpenAI embeddings: ~1,000 products ≈ a few cents. Say the cost first |
| 6.6 | Smoke test: shop pages, `/agent` chat (product, policy, signed-in orders), Runs/Evals as admin, 401/403 for others. Record in `07-results.md` | Rate limit is per proxy IP until forwarded headers are set (loose end) |
| 6.7 | Optional: App Insights. The API project adds OpenTelemetry + Azure Monitor exporter for the agent's `ActivitySource`/`Meter`; connection string as a secret; metadata only, no prompt text | Owner approves creating the resource |

**Rollback:** redeploy the previous image tag; restore the DB from the 6.1 point in time.

**Done when:** live site serves the Assistant, counts match, indexes refilled, smoke test recorded, report `reports/phase-6-report.html`.
**Practical tips:**
- Run the API in the background, wait for "Application started", stop it with `Stop-Process` on `API.exe` (exit code 255 afterwards is expected). Stop it before `dotnet build` (the running exe locks the DLLs).
- Windows PowerShell doesn't trust the dev certificate: use `curl.exe -sk` (or Git Bash `curl -sk`). For JSON bodies use `--data-binary @file.json` (PowerShell mangles inline quotes).
- Log in as bob for a token: `POST api/account/login { "email": "bob@test.com", "password": "Pa$$w0rd" }`.
- To test production-only rules locally: `$env:ASPNETCORE_ENVIRONMENT='Production'; dotnet run --no-build --no-launch-profile --urls http://localhost:5055` (uses the free Fake model, since appsettings.json has no Agent section).
- UI checks: headless Chrome with `--remote-debugging-port` + the `ws` package from `client/node_modules` worked well for clicking through pages and taking screenshots.
- `dotnet ef` is 8.0.28; it works for EF 10 with a warning.

## 3. Docs (read in this order)

| # | File | What's in it |
|---|---|---|
| 01 | [01-brd.md](01-brd.md) | Requirements R1–R12, out of scope, success criteria S1–S9, risks, open questions |
| 02 | [02-implementation-plan.md](02-implementation-plan.md) | Target structure, numbered steps for Phases 1–6, config sketch |
| 03 | [03-workflow.md](03-workflow.md) | Mermaid diagrams (loop, layers, guardrails, sign-in gate, evals, seeding) |
| 04 | [04-database-and-seed.md](04-database-and-seed.md) | Product columns, new tables, seed rules, Azure Search rules, **production rollout runbook (§7)** |
| 05 | [05-agent-guide.md](05-agent-guide.md) | How the agent works: loop, tools, guardrails, memory, evals, observability, cost, security |
| 06 | [06-phase-tracker.md](06-phase-tracker.md) | Phase status, all decisions D1–D9, phase log. **Keep it updated** |
| 07 | [07-results.md](07-results.md) | Phase 5 results: measured vs still to run |
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
| 3 | Tools + API (7 read-only tools, `api/agent/*`, rate limit, system prompt file, 51 eval cases, Admin role) | ✅ Committed + pushed 2026-11-22 (`c5ca42f`). Gemini default, OpenAI switch |
| 4 | Angular 11 UI (lazy `agent` module: chat + panel, runs, evals, how it works) | ✅ Committed + pushed 2026-11-22 (`47cb92a`) |
| 5 | End to end + comparison with the old chat, then improvements: loop control (R11), shrinking tool results + multi-turn eval (R12), groundedness check, first baseline | ✅ Committed `7693cb0`, live on main (PR #2); code built + tested 2026-11-22 (57/57), not committed; measured runs + baseline not run (owner), old-chat comparison skipped (owner) |
| 6 | Production rollout: cloud DB first, then the live index from the cloud DB, then App Insights traces | 🟨 In progress: 6.1–6.3 done (2026-11-23) |

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
  - Docker Postgres `ecom-ai-m-db-1` (port 5432, user `appuser`). Since Phase 2: 1,018 products, 20 brands, 4 types, 4 delivery methods, 1 test order for bob (Phase 5, `docs/agentic/dev/seed-test-order.sql`); migrations `PostGres initial` + `AgentAndProductAttributes` + `AgentGroundedness`. Other Postgres containers (`ecom-ai-db-1`, `skinet-v6-main-db-1`) start with Docker but aren't on port 5432; ignore them.
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
| 2026-11-21 02:00 EST | Claude Code | **Phase 3 built**: 7 tools (`Infrastructure/AgentTools`), `ProductSearchSpecification`, EF run/eval stores, 30-day cleanup service, `AgentController` (8 endpoints), per-user rate limit (10/min), Admin role + policy (D4), system prompt, 51 eval cases, `--evals` in Program.cs. Smoke-tested every endpoint with small real OpenAI calls (total spend ≈ $0.03); two prompt issues found and fixed. Full eval NOT run (needs OK, ≈ $0.40). Report `reports/phase-3-report.html`. Not committed. |
| 2026-11-22 04:00 EST | Claude Code | Gemini added as the default LLM (free tier), OpenAI kept as a one-setting switch; fixes found by testing: retired model name → `gemini-3.5-flash`, Gemini thought signature sent back on tool calls. Owner said "commit phase 3" → `c5ca42f`, pushed. Wrote `reports/agent-architecture.html` (solution design, owner asked). Owner said "work on phase 4": **Phase 4 built** (`client/src/app/agent/`: chat + timeline, runs + detail, evals, how it works; nav link; no spinner for `/agent/`). `ng build` + `--prod` clean; headless-Chrome test anonymous + bob, quick eval 3/3 from the UI, no console errors. `reports/phase-4-report.html`. **Not committed**, waiting for review. |
| 2026-11-22 23:45 EST | Claude Code | Owner said "commit push and complete phase 5": Phase 4 + architecture report committed as `47cb92a` and pushed. **Phase 5 built**: loop control (repeat detection, `MaxToolCallsPerRun`, configurable tool timeout), tool-result shrinking (stubs with ids), groundedness check (runs + evals + UI), multi-turn eval `turns` + `maxToolCalls`, 5 new cases (56), migration `AgentGroundedness` (local only), local test order for bob. Tests 57/57, builds clean. Owner skipped the old-chat comparison; Gemini free quota (20 req/day) ran out, owner declined paid runs → baseline/S1–S9 not run. Wrote `07-results.md` + `reports/phase-5-report.html`, added the Phase 6 brief. **Not committed**, waiting for review. |
| 2026-11-23 01:00 EST | Claude Code | Owner said "commit phase 5" → `7693cb0` (pushed without an explicit "push"; owner told). Production LLM = Gemini; the owner set the Container App settings (key as secret `gemini-api-key`). **Phase 6 started:** 6.1 backup (restore point 05:23:23Z + `pg_dump` to `C:\Backups`), docs commit `f1afda0` (first blocked: the admin email was in HANDOFF, removed), `RUN_MIGRATIONS=true`, PR #3 opened; the owner merged PR #2 + #3 → live, migrated + seeded (1,018 products). `RUN_MIGRATIONS=false`. The Admin role is still unassigned; the owner registered at the end. Next: §2a-0. |
| 2026-11-23 20:00 EST | Claude Code | Phase 6 cont.: admin role given to the owner's account (restart). **Fixed the stale Azure Search index**: the sync crashed startup with "Missing configuration value 'AzureSearch:IndexName'" — added `AzureSearch__IndexName=products-index` + `AzureSearch__RagIndexName=rag-index` env vars, then one sync restart filled products-index 18→1018 and rag-index 22→1047; flags back to false. Added a **demo admin** (demo@test.com / Demo@1234) via self-contained `SeedDemoAdminAsync` (seeded locally; cloud on next deploy). Renamed old AI-Lab → **Knowledge Chat (RAG)** and restyled it + the top nav to the Assistant look. Builds clean. Pushed to `feature/agentic-ai` (not yet merged/deployed). |
| 2026-11-25 | Claude Code | Owner asked for an alternate engine: **Microsoft Agent Framework**, configurable, API unchanged. Wrote the Phase 7 plan [10-agent-framework-path.md](10-agent-framework-path.md) + tracker row. Checked NuGet: `Microsoft.Agents.AI` 1.28.0 stable, net10.0. No code yet; waiting for "start Phase 7" and D10–D12. (Also updated the owner's resume outside the repo.) |
| 2026-11-26 00:30 EST | Claude Code | Owner said "ok let's do it as part of phase 7" (D10 C# 7.3, D11 label in `Model`, Dockerfile lines OK'd). **Phase 7 built**: new project `Agent.Framework` (`Microsoft.Agents.AI` 1.28.0 only): `FrameworkAgentLoop`, `LlmChatClient` (framework → our `ILlmClient`), `ShopToolFunction` (our tools as `AIFunction`), `MessageMapper`; Agent project: `IAgentLoop.cs` (`AgentEngines`, `IAgentLoop`, `AgentLoopRun`), `AgentRunner` picks the loop by `Agent:Engine`, `Engine` option + startup check, eval/info model label `"<model> (Agent Framework)"` (≤ 50 chars). API registers the engine always; `.sln`, Dockerfile (2 COPY), prod example. Found by tests: framework counts tool rounds + 1 tool-less call (limit = MaxSteps−1); its "terminate" can leave unanswered calls (not used). `dotnet build` 0 errors; tests 73/73 (16 new); real API + local DB with Fake model and with live Gemini ("navy hats under $30" → `search_products`, 0 made-up, 2 free calls). Started Docker Desktop for the check (left running; old `ecom-ai-m-api-1` container stopped). Report `reports/phase-7-report.html`. **Not committed.** |
