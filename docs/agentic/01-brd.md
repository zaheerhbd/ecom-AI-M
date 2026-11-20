# 01 – Business Requirements (BRD): Shop Assistant Agent

| | |
|---|---|
| Status | Draft for owner review (Phase 0) |
| Date | 2026-11-15 |
| Related | [02 Implementation plan](02-implementation-plan.md) · [03 Workflow](03-workflow.md) · [04 Database and seed](04-database-and-seed.md) · [05 Agent guide](05-agent-guide.md) · [06 Phase tracker](06-phase-tracker.md) |

## 1. Background

The shop already has an AI chat (`/api/ai/chat`, page `/ai-lab`). It works in fixed steps:

1. `AiChatService` guesses the intent from keywords ("delivery", "ship" → policy, otherwise product) and pulls a price limit from phrases like "under $50".
2. `AiRetrieverService` runs one vector search against Azure AI Search (`rag-index`).
3. A score gate decides whether the matches are good enough.
4. `OpenAiAnswerGeneratorService` writes the answer from the matches.

**Problems with this design**

- **Brittle intent.** A question that mixes topics ("cheapest hat and how fast can it arrive?") goes down only one path.
- **One search, no follow-up.** The model can't ask a second question, filter by brand or sort by price. Questions like "cheapest", "fastest" or "how many" depend on luck in vector ranking.
- **No exact data.** Prices, stock and order status come from embedded text, not from the database.
- **No visibility.** You can't see why an answer came out the way it did, what it cost, or how long each part took.
- **Too little data.** 18 products with lorem-ipsum descriptions can't show realistic search behaviour.

## 2. Goal

Replace the fixed pipeline with a **tool-using agent**. The model decides which tools to call (product search with real SQL filters, product details, policy search, delivery options, the user's own orders), reads the results and writes the answer. Every step must be **visible**: guardrails, model calls, tool calls, tokens, cost and latency.

The engine is based on the owner's learning project **SimpleAgent** (`C:\Agents\SimpleAgent`). The code must stay easy to read for a learner.

## 3. Users

| User | Need |
|---|---|
| Shopper (anonymous) | Find products ("waterproof gloves under $40"), ask about delivery, returns and payments. |
| Shopper (signed in) | Everything above, plus "where is my last order?" |
| Owner / developer (learner) | See exactly how the agent reached an answer. Compare runs, run evals, learn agent concepts. |

## 4. Scope

### In scope

| # | Requirement |
|---|---|
| R1 | A new **Agent engine** project in the solution, copied from SimpleAgent.Core and made generic. It has no reference to Core, Infrastructure or API. |
| R2 | **Read-only tools** over the real shop data: product search, product details, catalog filters, knowledge/policy search, delivery options, my orders, one order. |
| R3 | **Order tools only for signed-in users.** The user's identity always comes from the JWT, never from what the model says. |
| R4 | **Guardrails** on input and output (length, prompt injection, PII redaction, secret/canary leak). Each check is recorded with its outcome: passed, modified, blocked or skipped. |
| R5 | **More data:** about 1,000 realistic products (more brands, colours, materials, stock, ratings) plus policy documents (returns, warranty, payments, international shipping, discounts, support). |
| R6 | **Run history:** every agent run is saved to the database with its steps, tokens, cost and latency. |
| R7 | **Evals:** about 45 test cases that can be run from the UI or the command line, with results saved and shown.<br>• **Cases:** the 21 ported retrieval cases, plus multi-tool (e.g. "cheapest hat and how fast can it arrive?"), ambiguous (should ask a question or offer options), should refuse / say "I don't know", safety, order (signed in vs anonymous) and multi-turn cases.<br>• **Every case lists its expected tools and the arguments each must include** (`expectedTools`), so the "correct tool" rate can be measured.<br>• **Several runs per case** (`repeat`: 3 by default, 5 for reports), shown as "5/5", "4/5", with flaky cases flagged.<br>• **Regression gate:** an eval command compares the run with a saved baseline (`evals/baseline.json`) and fails (exit code 1) when the pass rate drops more than 5 points. It is run before merging. Running it automatically on every GitHub pull request is a later option (it needs the OpenAI key as a secret, a Postgres service and paid calls on each PR).<br>• **Groundedness check (Phase 5):** code, not an LLM, pulls every price and stock number out of the answer and checks it appears in that run's tool outputs. A number that doesn't is counted as made up. |
| R8 | **UI (Angular 11):** a chat page with a live observability panel, a runs list, an evals page and a "how it works" page. A nav link opens them. |
| R9 | The old `/api/ai/chat` keeps working until the comparison in Phase 5 is done. |
| R10 | A per-user/IP **rate limit** on the agent endpoint. |
| R11 | **Agent loop control** *(improvement, delivered in Phase 5)*. Phase 1 copies the engine's existing behaviour as is: `MaxSteps`, a token budget, a 10 s tool timeout, and tool errors returned to the model as data. Phase 5 adds:<br>• **`MaxToolCallsPerRun`** (default 10): a cap on tool calls across the whole run, because one turn can ask for many tools.<br>• **`ToolTimeoutSeconds`** in config (default 10) instead of hard-coded.<br>• **Error steps flagged** (`IsError`) so the UI shows them in red. The run is **not** aborted; the model can fix its call or explain the problem.<br>• **Repeated-call detection:** the same tool with the same arguments (keys sorted) in one run is not executed again. The model gets "You already called this; use the earlier result", and the repeat is shown as a step.<br>• **Tools run sequentially.** The model may ask for several in one turn, but they run one after another (they share one scoped `DbContext`, and the trace stays clear). |
| R12 | **Multi-turn memory.** The agent remembers earlier messages in the same chat session, so "Where's my last order?" followed by "When will it arrive?" works.<br>• **Phase 1** (already in the engine): history stored per `sessionId` in server memory; the system prompt is always kept, plus the last 20 messages; a trim never splits a tool call from its result; a session is tied to the signed-in user's email.<br>• **Phase 5 (improvement): shrink old tool results.** When a turn is saved, raw tool results from that turn are replaced with a short stub that keeps the ids, e.g. `[search_products: 10 items, ids 12, 45, 88]` or `[get_order: order 7, Shipped]`. Raw results are kept in full only while the current turn runs.<br>• The trace shows input tokens per LLM call, so the growth of the history and the saving from the stubs are visible.<br>• A **two-turn eval case** proves it works. |

### Out of scope (for now)

- **Write actions:** adding to basket, placing or cancelling orders. They are not built, but a one-page design with human approval is in [09-write-actions-design.md](09-write-actions-design.md).
- **MCP server** for the shop tools (owner's decision, 2026-11-15).
- **Agent frameworks** (Semantic Kernel, Agent Framework, Microsoft.Extensions.AI): not used; see the ADR in [08-adr-why-build.md](08-adr-why-build.md).
- Streaming responses (the answer arrives whole).
- Multi-language support.
- Publishing the engine as a NuGet package.
- New product images. The 4 existing types (Boards, Hats, Boots, Gloves) are kept because images exist only for those.
- Overwriting the shared Azure AI Search index (see constraints).
- **Memory extras, not planned:** summarising old conversation with the LLM (an extra call each turn, and it can lose details like order ids); memory that survives a server restart (it can be added later behind `IConversationStore`); long-term memory about a user across sessions.
- **Loop-control extras, not planned:** running tools in parallel; aborting the run when a tool fails; stopping the run after N repeated calls (`MaxSteps` and `MaxToolCallsPerRun` already bound it); per-tool timeouts, retries or circuit breakers; detecting "similar" (not identical) repeated calls.

## 5. Success criteria

| # | Measure | Target |
|---|---|---|
| S1 | Eval pass rate on the ported 21 cases | Higher than the old chat on the same cases (baseline: `docs/ai-retrieval-test-results-*.csv`) |
| S2 | Safety cases (injection, prompt leak, other user's order) | 100% pass, in every run |
| S3 | Correct tools and arguments (`expectedTools`) | ≥ 90% |
| S4 | Latency per question | Median ≤ 6 s, **p95 ≤ 12 s** (measured and recorded, not a hard gate) |
| S5 | Cost per question | Shown on every run; **median ≤ 8k tokens** per question (a token target doesn't change when prices change; the dollar figure is worked out from it in Phase 1) |
| S6 | Every run visible in the runs list with all its steps | 100% |
| S7 | Consistency | Each case passes **≥ 4 of 5** runs |
| S8 | Groundedness (Phase 5) | ≥ 98% of prices and stock numbers in answers match a tool result, so the made-up rate is ≤ 2% |
| S9 | No regression | The pass rate never drops more than 5 points against the saved baseline |

## 6. Constraints

- **Code style:** classic C# 7.3 syntax in the Agent project (`<LangVersion>7.3</LangVersion>`, `Nullable` and `ImplicitUsings` disabled). The code is commented in plain English for a learner.
- **Layering:** the Agent project must never reference Core, Infrastructure or API. Tools live in `Infrastructure/AgentTools/` and wrap existing services.
- **Azure AI Search is shared with the deployed app.** Don't run `--sync-rag` or `--sync-products` against it from the laptop. The new products reach the live index by loading the **cloud database first** and then syncing the index **from the cloud database** (Phase 6). The live index then matches the live site, and every product gets embeddings.
- **Paid API calls** (e.g. writing 1,000 product descriptions with the LLM) need the owner's approval of the cost first.
- **Angular 11 needs Node 16** (`nvm use 16.20.2`).
- **Work in phases.** Each phase starts only when the owner says so, and every coding phase ends with a code review.

## 7. Risks

| Risk | Mitigation |
|---|---|
| The model calls the wrong tool or makes up facts | Clear tool descriptions, a system prompt that says "always use a tool", evals that check the tool and its arguments |
| A shopper sees another shopper's orders | User email comes only from the JWT in `AgentRequestContext`; order tools are hidden from anonymous users; a safety eval tests it |
| Prompt injection through the user's text or through tool results | Input guardrail, canary check on output, tool results treated as data in the system prompt |
| Cost grows unnoticed | Token budget per message, cost shown per run, totals on the stats endpoint |
| Seeding 1,000 products breaks existing data | Idempotent seeding that only adds missing rows; a DB backup is taken before Phase 2 |
| **Known limitation:** `search_products` matches words, not meaning, so "waterproof" won't find "seam-sealed Gore-Tex" | Accepted for now (D9). The model can widen a search itself (e.g. try `material: Gore-Tex`), and seed descriptions use plain words like "waterproof". Feature tags or hybrid search can be added later. |
| Reasoning models (gpt-5 family) reject a custom temperature | Temperature is optional in config and left out when not set |

## 8. Open questions for the owner

1. Product descriptions: LLM-written once (cost to be confirmed before the call) or templates only?
2. Rate limit values: suggested 10 requests/minute per user or IP. OK?
3. How long to keep agent runs: forever, or delete after N days?
4. Who can open the runs and evals pages: everyone in dev, but should they need sign-in (or an admin role) in production?
