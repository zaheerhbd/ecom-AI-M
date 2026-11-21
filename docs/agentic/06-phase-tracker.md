# 06 – Phase Tracker

Update this file at the end of every phase. Status values: ⬜ Not started · 🟦 In progress · 🟨 Waiting for review · ✅ Done

**Every phase ends with an HTML report** in [reports/](reports/) (`phase-N-report.html`): an easy-to-read, navigable page that explains in plain English what was built, how to check it, and what's next. Link it in the phase log below.

| Phase | Name | Status | Started | Finished | Review approved | Notes |
|---|---|---|---|---|---|---|
| 0 | Docs | ✅ | 2026-11-15 | 2026-11-15 | 2026-11-16 | Approved when the owner said "work on Phase 1" |
| 1 | Agent engine project | ✅ | 2026-11-16 | 2026-11-16 | 2026-11-19 | Built and tested (38/38). Approved and committed when the owner said "commit phase 1". Report: [phase-1-report.html](reports/phase-1-report.html) |
| 2 | Schema and seed | ✅ | 2026-11-19 | 2026-11-19 | 2026-11-21 | Local DB migrated + seeded (1,018 products), seed idempotent. Committed and pushed with the owner's OK. Report: [phase-2-report.html](reports/phase-2-report.html) |
| 3 | Tools and API | ⬜ | | | | No Azure sync without approval |
| 4 | Angular 11 UI | ⬜ | | | | Node 16 (`nvm use 16.20.2`) |
| 5 | End to end, comparison, loop-control improvements | ⬜ | | | | Owner decides on retiring `AiChatService`; then R11 loop control (steps 5.5–5.7) and R12 tool-result shrinking + two-turn eval (5.8–5.9), groundedness check + first baseline (5.10–5.11) |
| 6 | Production rollout | ⬜ | | | | Cloud DB first, then live index from the cloud DB ([04 §7](04-database-and-seed.md#7-production-rollout-cloud-database-then-index)). Each step only when the owner says so. Then App Insights traces (6.7) |

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

(07 is reserved for `07-results.md`, written in Phase 5.)

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
