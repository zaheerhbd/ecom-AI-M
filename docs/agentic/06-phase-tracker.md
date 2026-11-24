# 06 – Phase Tracker

Update this file at the end of every phase. Status values: ⬜ Not started · 🟦 In progress · 🟨 Waiting for review · ✅ Done

**Every phase ends with an HTML report** in [reports/](reports/) (`phase-N-report.html`): an easy-to-read, navigable page that explains in plain English what was built, how to check it, and what's next. Link it in the phase log below.

| Phase | Name | Status | Started | Finished | Review approved | Notes |
|---|---|---|---|---|---|---|
| 0 | Docs | ✅ | 2026-11-15 | 2026-11-15 | 2026-11-16 | Approved when the owner said "work on Phase 1" |
| 1 | Agent engine project | ✅ | 2026-11-16 | 2026-11-16 | 2026-11-19 | Built and tested (38/38). Approved and committed when the owner said "commit phase 1". Report: [phase-1-report.html](reports/phase-1-report.html) |
| 2 | Schema and seed | ✅ | 2026-11-19 | 2026-11-19 | 2026-11-21 | Local DB migrated + seeded (1,018 products), seed idempotent. Committed and pushed with the owner's OK. Report: [phase-2-report.html](reports/phase-2-report.html) |
| 3 | Tools and API | ✅ | 2026-11-21 | 2026-11-22 | 2026-11-22 | 7 tools, 8 endpoints, rate limit, Admin role, 51 eval cases, Gemini provider (default) + OpenAI switch. Smoke-tested; full eval not run. Committed when the owner said "commit phase 3". Report: [phase-3-report.html](reports/phase-3-report.html) |
| 4 | Angular 11 UI | ✅ | 2026-11-22 | 2026-11-22 | 2026-11-22 | Lazy `/agent` module: chat + step timeline, runs + detail, evals, how it works; "Assistant" nav link. Tested in headless Chrome. Committed as `47cb92a` and pushed when the owner said "commit push". Report: [phase-4-report.html](reports/phase-4-report.html) |
| 5 | End to end, comparison, loop-control improvements | ✅ | 2026-11-22 | 2026-11-22 | – | R11 loop control, R12 shrinking, groundedness (S8), multi-turn evals, 56 cases, tests 57/57. **Measured runs not done** (Gemini free = 20 calls/day; owner declined paid runs); old-chat comparison skipped by the owner. Committed `7693cb0`; merged to `main` (PR #2) and live 2026-11-23. Results: [07-results.md](07-results.md). Report: [phase-5-report.html](reports/phase-5-report.html) |
| 6 | Production rollout | 🟨 | 2026-11-23 | | | Cloud DB first, then live index from the cloud DB ([04 §7](04-database-and-seed.md#7-production-rollout-cloud-database-then-index)). Each step only when the owner says so. Then App Insights traces (6.7) |

## Documents

| # | File | Purpose |
|---|---|---|
| – | [HANDOFF.md](HANDOFF.md) | Carry-over from the planning session |
| 01 | [01-brd.md](01-brd.md) | Business requirements, scope, success criteria |
| 02 | [02-implementation-plan.md](02-implementation-plan.md) | Step-by-step plan for each phase |
| 03 | [03-workflow.md](03-workflow.md) | Mermaid diagrams |
| 04 | [04-database-and-seed.md](04-database-and-seed.md) | Schema changes, new tables, seed rules |
| 05 | [05-agent-guide.md](05-agent-guide.md) | How the agent works: loop, tools, guardrails, memory, evals, observability, cost, security |
| 06 | this file | Progress |
| 08 | [08-adr-why-build.md](08-adr-why-build.md) | ADR: why build our own engine instead of Semantic Kernel / Agent Framework / Microsoft.Extensions.AI |
| 09 | [09-write-actions-design.md](09-write-actions-design.md) | Design only: "add to basket" with human approval, undo and audit |

| 07 | [07-results.md](07-results.md) | Phase 5 results: what is measured, what is still to run |

## Decisions waiting for the owner

| # | Question | From | Answer |
|---|---|---|---|
| D1 | Product descriptions: LLM-written (cost confirmed first) or templates only? | BRD §8 | **Templates for Phase 2** (2026-11-19, the plan's free default, since Phase 2 started without an answer). The LLM option stays open; it needs a cost check and the owner's OK. |
| D2 | Rate limit: 10 requests/minute per user or IP? | BRD §8 | **Decided 2026-11-20: per user** (10/minute). Anonymous visitors have no user, so they fall back to their IP. |
| D3 | How long to keep agent runs? | BRD §8 | **Decided 2026-11-20: 30 days.** |
| D4 | Restrict the runs/evals pages in production (sign-in or admin)? | BRD §8 | **Decided 2026-11-16: admin only** in production (open to all in dev). |
| D5 | Add an `Agent.Tests` xUnit project? | Plan, Phase 1 | **Decided 2026-11-19: kept** (added in Phase 1, approved with the Phase 1 commit). |
| D6 | Keep the Chat Completions API for the agent client (Responses API stays for the old chat)? | Plan, open technical decisions | **Decided 2026-11-19: kept** (approved with the Phase 1 commit). |
| D7 | Separate dev Azure Search index, or local policy fallback only? | DB doc §6 | **Decided 2026-11-15: load the cloud DB first, then fill the live index from it (Phase 6).** **No separate `-dev` indexes**, so the laptop never syncs to Azure Search; local work uses the local policy fallback. |
| D9 | Product search by meaning ("waterproof" → "Gore-Tex"): feature tags or hybrid search? | Review | **Decided 2026-11-15: neither for now.** `search_products` stays SQL; known limitation, see BRD §7 |
| D8 | Product images: reuse the 18 existing images, or generate new ones? | DB doc §4 | **Decided 2026-11-15: reuse the existing images for now.** Later option: 48 images (4 types × 12 colours), made in the Gemini app or through a paid image API |

## Phase log

### Phase 0 – Docs (2026-11-15)
- Wrote 01–06 in `docs/agentic/`.
- No code, config, database or git changes.
- Review rounds added R11 (loop control), R12 (memory), stronger evals, Phase 6 (production rollout + App Insights), doc 08 (ADR) and doc 09 (write-actions design). D7, D8 and D9 decided; MCP skipped.
- HANDOFF.md rewritten as the single entry point for future sessions (2026-11-15 04:56 EST).
- Next: the owner reviews the docs, answers D1–D6 and says when Phase 1 starts.

### Phase 1 – Agent engine (2026-11-16)
- Branch `feature/agentic-ai` created from `main`. Nothing committed yet.
- New project `Agent/` (net10.0, C# 7.3, no NuGet packages, no references to Core/Infrastructure/API) and test project `Agent.Tests/` (xUnit). Both added to `eCom.sln`.
- Steps 1.1–1.14b done: config-driven options, `AddAgent(...).AddTool<T>()`, scoped runner/registry/tools/context, `AgentRequestContext`, `IRequiresSignIn` (hidden + refused), typed parameters and `GetInt`/`GetNumber`/`GetBool`, `GuardrailCheck`, richer `AgentStep` and `AgentResult`, `IAgentRunStore` + in-memory store, session-owner check, eval runner (repeat, flaky, judge, median/p95), command-line regression gate, `gen_ai.*` telemetry names.
- Results: `dotnet build eCom.sln` passes (0 errors; the 5 warnings are in existing API code). `dotnet test Agent.Tests`: **38/38 passed**.
- Not checked: starting the API, because Docker Desktop was not running. The API project itself has no changes.
- Small changes from the plan (docs updated): `ITool.ExecuteAsync` also receives `AgentRequestContext`; LLM settings live in `Agent:Llm`; extra fields `StopReason` on runs and repeat/flaky/latency fields on eval runs (see [04](04-database-and-seed.md)); default baseline path `Agent/evals/baseline.json`.
- Report: [reports/phase-1-report.html](reports/phase-1-report.html).
- Next: the owner reviews the code. Then Phase 2 (schema and seed) when the owner says so.
- 2026-11-19: owner approved ("commit phase 1 and lets go with phase 2"). Phase 1 committed on `feature/agentic-ai` as `4ab0817` (not pushed). D5 and D6 confirmed.

### Phase 2 – Schema and seed (2026-11-19)
- 2.1 Backups in a permanent folder: `C:\Backups\skinet-before-phase2.sql`, `C:\Backups\identity-before-phase2.sql`. Counts before: 18 products, 6 brands, 4 types, 4 delivery methods, 0 orders.
- 2.2–2.4 `Product` got `Color`, `Material`, `StockQuantity`, `Rating` (also in `ProductToReturnDto`). Tables `AgentRuns`, `EvalRuns`, `EvalCaseResults` map the Agent record classes; lists are `jsonb` via `Data/Config/JsonColumn.cs`. Migration `20261120044648_AgentAndProductAttributes` (sets stock 25 / rating 4.0 on existing rows). `Infrastructure` now references `Agent`.
- 2.5–2.6 `tools/seed-gen/generate.js` (fixed seed, output identical on re-run): 1,018 products (18 originals unchanged + 1,000 new, 96 out of stock), 20 brands. Template descriptions (D1).
- 2.7 Six policy files in `Infrastructure/Data/SeedData/policies/` (invented text; owner to review).
- 2.8 `StoreContextSeed` is idempotent: adds by name, never updates/deletes; sets ids for new brands/types itself (the original rows were inserted with fixed ids, so the DB counter is behind).
- 2.9 `AiDocumentService` includes policies through the new `PolicyDocumentLoader` (one document per section). No Azure sync.
- Results: first `--migrate` added 14 brands + 1,000 products; second added 0. MD5 fingerprint of the original 18 products unchanged. API starts (closes the Phase 1 loose end); `/api/products` count 1018 with the new fields; `/api/ai/documents` 1,018 products + 29 policy documents. Build 0 errors, tests 38/38.
- `Dockerfile`: added `COPY Agent/Agent.csproj Agent/` and `COPY Agent/. Agent/` (Infrastructure now references Agent) after the owner said OK (2026-11-20). The owner's own uncommitted edits in that file are untouched. Owner also OK'd the policy text, and decided D2 (per user) and D3 (30 days).
- Report: [reports/phase-2-report.html](reports/phase-2-report.html).
- 2026-11-21: owner said "commit and push". Committed with the `Dockerfile`, the `global.json` rollForward fix and a shop UI tidy-up (separate commit), pushed. The deploy pipeline was fixed the same day (PR #1 to `main`, see HANDOFF §2a-bis).

### Phase 3 – Tools and API (2026-11-21)
- 3.1 Seven read-only tools in `Infrastructure/AgentTools/`: `search_products`, `get_product_details`, `list_catalog_filters`, `search_knowledge`, `get_delivery_options`, `get_my_orders`*, `get_order`* (*`IRequiresSignIn`; email only from the JWT).
- 3.2 `ProductSearchParams` + `ProductSearchSpecification` (brand, type, colour, material, price range, in stock, min rating; sort price/rating/name). Unknown brand/type → list of valid names.
- 3.3 `search_knowledge`: local keyword search over the policy files (`LocalPolicySearch`); Azure only when `Agent:Knowledge:UseAzureSearch` is true (default false, D7). Result says `source: local|azure`.
- 3.4 `EfAgentRunStore`, `EfEvalRunStore`; `AgentRunCleanupService` deletes runs older than 30 days (D3).
- 3.5–3.7 `AgentController` (chat, info, runs, runs/{id}, stats, evals POST/GET, evals/{id}); request context filled from the JWT in the controller; built-in rate limiter, 10/min per user, IP for anonymous (D2); `AgentAdmin` policy: open in Development, Admin role elsewhere (D4). Admin role: roles switched on in Identity, role claims in the JWT, `SeedAdminRoleAsync` for `Agent:AdminEmails` on `--migrate`.
- 3.8 `API/Agent/system-prompt.md`; `Agent` config in `appsettings.Development.json` (local) and `appsettings.Production.json.example`. Pricing gpt-5.4-mini $0.75 / $4.50 per 1M tokens (2026-11-21).
- 3.9 `API/Agent/evals/cases.json`: 51 cases (21 ported, 5 product, 6 multi-tool, 6 ambiguous, 4 decline, 6 safety, 3 orders).
- 3.10 Smoke tests passed (see report). Testing found two prompt issues (signed-in user told to sign in; anonymous "my orders" made a pointless tool call), both fixed. `Program.cs`: `dotnet run -- --evals` works (3/3 small run).
- Not done / for the owner: full eval run (≈ $0.40), test order for bob, commit.
- Report: [reports/phase-3-report.html](reports/phase-3-report.html).
- 2026-11-22: owner asked for Gemini (free tier) as the default with OpenAI kept as a switch. `Agent:Llm:Provider` = `Gemini` | `OpenAI`; keys/models per provider section. Fixes found by testing: retired model name (now `gemini-3.5-flash`), Gemini's thought signature must be sent back on tool calls. 3 questions answered correctly at $0. Tests 43/43.
- 2026-11-22: owner said "commit phase 3". Committed as `c5ca42f` and pushed.

### Phase 4 – Angular UI (2026-11-22)
- 4.1–4.2 Lazy `AgentModule` at `/agent` (`client/src/app/agent/`) with a tab bar; `AgentService` + models matching the Phase 3 JSON.
- 4.3 Chat page: messages + "How this answer was made" panel (`AgentTimelineComponent`: totals, guardrail chips, expandable LLM/tool steps with pretty JSON, sources). Session id and messages kept per tab (`sessionStorage`); suggestions; 429 and "server down" notices; signed-in banner. Answers formatted with a small escape-first Markdown pipe.
- 4.4 Runs page (stats cards, most used tools, paged table, "test" badge for eval runs) and run detail (reuses the timeline).
- 4.5 Evals page: quick check (PS-001, TC-018, SF-001) by default, or a category, case ids, or all 51 with a confirm tick; repeat 1–3; optional judge; progress bar (polls every 3 s); results with per-check chips and a link to each answer's run; earlier runs list.
- 4.6 How it works: plain-English steps, loop SVG, safety list, live model/limits/tools from `api/agent/info`.
- 4.7 "Assistant" nav link. The shop's full-screen loading spinner is skipped for `/agent/` calls.
- Results: `ng build` and `ng build --prod` 0 errors, 0 warnings. Headless-Chrome run: anonymous chat + follow-up (memory OK), tool step expands, runs + detail, quick eval 3/3 from the UI, how it works, signed in as bob → `get_my_orders`; no console errors. All on Gemini's free tier ($0).
- Not tested in the browser: 429 notice, production admin-only messages (API side tested in Phase 3). No Angular unit tests added.
- No API, database or settings changes.
- Report: [reports/phase-4-report.html](reports/phase-4-report.html). Also new on 2026-11-22: [reports/agent-architecture.html](reports/agent-architecture.html) (solution design). Not committed.
- 2026-11-22: owner said "commit push". Committed as `47cb92a` (with `agent-architecture.html`) and pushed.

### Phase 5 – Loop control, memory, groundedness (2026-11-22)
- Owner decisions: skip the old-chat comparison (5.3/5.4); baseline on free Gemini. Gemini's free tier turned out to be **20 requests/day per model**, used up today, so the owner chose **not to run** paid OpenAI evals: 5.2/5.11 (scores, S1–S9, baseline) are ready but not run.
- 5.5 `MaxToolCallsPerRun` (10), `ToolTimeoutSeconds` (10, now from config), identical calls (tool + sorted args) not run again (`AgentStep.IsRepeat`), stop reason `max-tool-calls`; every refused call still gets a tool result so the chat stays valid.
- 5.6 Timeline: repeats amber, groundedness note, new stop reason; Evals page: made-up-rate card.
- 5.7 Cases `LC-001` (unknown brand → recovers), `LC-002` (`maxToolCalls` 1).
- 5.8 `ToolResultSummariser` + optional `ISummarisingTool`; `ShrinkOldToolResults` (default on). Fake-model measure: −182 input tokens (−16%) per later message.
- 5.9 `turns` in eval cases (one session, last answer graded); `OR-004`, `MM-001`, `MM-002`; local test order for bob via `docs/agentic/dev/seed-test-order.sql`.
- 5.10 `Groundedness` (regex, no LLM): `GroundedFactCount` / `MadeUpFacts` on runs, `FactsChecked` / `MadeUpFacts` / `MadeUpRate` on eval runs; eval check only when an answer has a price/stock number. Migration `AgentGroundedness` (local DB applied; cloud in Phase 6).
- Results: build 0 errors, `Agent.Tests` 57/57 (14 new; one old test adjusted for repeat detection), `ng build` 0 errors. Live Gemini check of the new cases: HTTP 429 (quota). Committed + pushed as `7693cb0` (2026-11-22).
- Report: [reports/phase-5-report.html](reports/phase-5-report.html). Results: [07-results.md](07-results.md).

### Phase 6 – Production rollout (started 2026-11-23)
- Before 6.1, Container App settings (owner, via portal): `Gemini__ApiKey` → secret `gemini-api-key`; `Agent__Llm__Provider` = Gemini; `Gemini__Model` = gemini-3.5-flash; `Agent__AdminEmails__0` = owner's email. Revision `--0000021` healthy. No effect until the new image is deployed (6.2).
- Found: all other Container App settings (connection strings, `Token__Key`, `OpenAI__ApiKey`, `AzureSearch__ApiKey`) are plain values, not secrets. Works; move to secrets later (loose end).
- **6.1 backup (2026-11-23):** Azure point-in-time restore point **2026-11-23T05:23:23Z** (automatic backups, 7 days kept). `pg_dump` (v18, via the local Docker container, `sslmode=require`) → `C:\Backups\skinet-before-phase6.sql` (13,940 bytes) and `C:\Backups\identity-before-phase6.sql` (13,186 bytes), both complete. Temporary firewall rule for the laptop IP added and removed again.
- Cloud state before rollout — `skinet`: Products 18, ProductBrands 6, ProductTypes 4, DeliveryMethods 4, Orders 0, OrderItems 0; last migration `20210226053111_PostGres initial` (the Phase 2 and Phase 5 migrations are not there yet). `identity`: Users 4, Roles 0; last migration `20210226053226_PostGres identity initial`.
- **6.2 deploy (2026-11-23):** `RUN_MIGRATIONS=true` set first (old image restarted harmlessly; its seed only fills empty tables). The owner merged PR #2 (05:32 UTC, Phases 1–5) and PR #3 (05:38 UTC, 6.1 notes); both deploy runs succeeded. `main` = `675f946`.
- **6.3 migrate + seed:** done by PR #2's first start (revision 23); revision 24 found nothing left to do. Checked on the cloud DB: Products 1,018 (ids 1–18 kept), Brands 20, Types 4, DeliveryMethods 4, Orders 0, Users 4; migrations `AgentAndProductAttributes` + `AgentGroundedness` applied; new columns present; role `Admin` created with **0 members** (the owner's account didn't exist yet). `RUN_MIGRATIONS` back to `false`, firewall rule removed, revision `--0000025` healthy.
- Still to do: give the owner the Admin role (the owner registered at the end of the session; one more `RUN_MIGRATIONS=true` start), 6.4 index schema, 6.5 refill indexes, 6.6 smoke test, report.
