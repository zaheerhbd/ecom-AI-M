# 02 – Implementation Plan

Each phase starts **only when the owner says so**. Each coding phase ends with a **code review**: list the changed files, stop, and wait for approval. Work happens on the branch `feature/agentic-ai`. Commit or push only when asked.

**Every review checkpoint also delivers an HTML phase report** at `docs/agentic/reports/phase-N-report.html`: one self-contained, easy-to-read page with a table of contents, explaining in plain English what was built, how it works, how to check it, what changed from the plan, and what's next. Use [reports/phase-1-report.html](reports/phase-1-report.html) as the template, and link the report from the phase log in [06](06-phase-tracker.md).

## Target structure

```
eCom.sln
├── Agent/                         NEW engine project (no refs to Core/Infrastructure/API)
│   ├── Agent.csproj               net10.0, LangVersion 7.3, Nullable/ImplicitUsings disable
│   ├── AgentRunner.cs             the loop
│   ├── AgentOptions.cs            prompt file, app name, fallback, limits, pricing
│   ├── AgentResult.cs             answer, steps, guardrail checks, sources, tokens, cost
│   ├── AgentRequestContext.cs     user email (from JWT), channel, collected sources
│   ├── Guardrails/                IGuardrail, pipeline, 4 guardrails, GuardrailCheck
│   ├── Llm/                       ILlmClient, OpenAiLlmClient, FakeLlmClient, models
│   ├── Memory/                    IConversationStore + in-memory store
│   ├── Tools/                     ITool, IRequiresSignIn, Schema helpers, ToolRegistry
│   ├── Runs/                      IAgentRunStore, AgentRunRecord
│   ├── Evals/                     EvalCase, Graders, EvalRunner, EvalRunRecord
│   └── ServiceCollectionExtensions.cs   AddAgent(config).AddTool<T>()
├── Infrastructure/
│   ├── AgentTools/                NEW tools wrapping existing services
│   ├── Data/AgentRunStore.cs      NEW EF implementation of IAgentRunStore
│   └── Data/SeedData/policies/*.md   NEW policy documents
├── API/
│   ├── Controllers/AgentController.cs   NEW api/agent/*
│   └── Agent/system-prompt.md, Agent/evals/cases.json
└── client/src/app/agent/          NEW lazy Angular module
```

Dependency direction: `API → Infrastructure → Core`, and `API → Agent`, `Infrastructure → Agent`. **Agent → nothing.**

---

## Phase 0 – Docs ✅ (this set)

BRD, plan, workflow, database and seed, agent guide, phase tracker.

## Phase 1 – Agent engine project

**Goal:** a building, tested engine that knows nothing about the shop.

| Step | Work |
|---|---|
| 1.1 | Create `Agent/Agent.csproj` (net10.0, C# 7.3, `FrameworkReference Microsoft.AspNetCore.App`, no NuGet packages). Add it to `eCom.sln`. |
| 1.2 | Copy the SimpleAgent.Core files (Agent, Guardrails, Llm, Memory, Observability, Tools: interfaces and registry only). Rename the namespace to `Agent.*`. Leave out the BookNook demo tools. Memory (`IConversationStore`, trimming to the last 20 messages) is copied as is (BRD R12). Add a check that a session id belongs to the user who started it. |
| 1.3 | **Config-driven options:** `AppName`, `SystemPromptPath` (or inline `SystemPrompt`), `FallbackAnswer`, `MaxSteps`, `MaxTokensPerMessage`, `MaxHistoryMessages`, `MaxInputChars`, `Canary`, optional `Temperature` (`double?`; left out of the request when null, because gpt-5 reasoning models reject it), `Pricing` (input/output price per million tokens). |
| 1.4 | **Registration API:** `services.AddAgent(config).AddTool<SearchProductsTool>()...` returns an `AgentBuilder`. |
| 1.5 | **Lifetimes:** `AgentRunner`, `ToolRegistry`, tools and `AgentRequestContext` are **scoped** (shop services use a DbContext). LLM client, guardrails and conversation store stay singleton. |
| 1.6 | **`AgentRequestContext`** (scoped): `UserEmail` (null when anonymous), `IsSignedIn`, `Channel`, `Sources` (list the tools add to). |
| 1.7 | **`IRequiresSignIn`** marker interface. `ToolRegistry.GetDefinitions(context)` hides these tools from anonymous users. `ExecuteAsync` refuses them too, as a second line of defence. |
| 1.8 | **Typed parameters:** `ToolParameter` gains `Type` (string, number, integer, boolean) and `Enum` values. Add `GetNumber`, `GetInt` and `GetBool` helpers that accept both `5` and `"5"`. |
| 1.9 | **`GuardrailCheck`** record: `Stage` (input/output), `Name`, `Outcome` (Passed, Modified, Blocked, Skipped), `Reason`, `DurationMs`. Replaces the plain `"name: ok"` strings. |
| 1.10 | **Richer `AgentStep`:** `Index`, `Type` (guardrail, llm, tool), `Name`, `Input`, `Output`, `InputTokens`, `OutputTokens`, `DurationMs`, `IsError`. |
| 1.11 | **`AgentResult`** gains `Sources`, `GuardrailChecks`, `EstimatedCostUsd`, `Model`, `RunId`. |
| 1.12 | **`IAgentRunStore`** with `SaveAsync`, `GetAsync(id)`, `ListAsync(page, size)` and `GetStatsAsync()`. The runner saves each run at the end (a store failure is logged and never breaks the answer). Ship an `InMemoryAgentRunStore` for tests. |
| 1.13 | **Eval runner** (moved from the console app into the engine): runs cases in the background, one DI scope per run. Checks: `expectedTools` (each listed tool was called **with** the listed arguments), `mustContain` / `mustNotContain`, `shouldBlock`, `shouldClarify` / `shouldDecline`, and an optional LLM judge (1–5). **`repeat`** runs each case N times (default 3) and reports passes out of N, with a flaky flag. Results include median and p95 latency and median tokens. Progress is saved through `IEvalRunStore`. |
| 1.13b | **Baseline / regression gate:** `dotnet run --evals [--repeat 5] [--baseline Agent/evals/baseline.json]` runs all cases from the command line, prints a summary, and exits with code 1 when the pass rate drops more than 5 points below the baseline. `--save-baseline` writes a new baseline. |
| 1.14 | Small xUnit test project `Agent.Tests` using `FakeLlmClient`: loop, max steps, token budget, guardrail block, sign-in hiding, argument parsing. *(Ask the owner: OK to add a test project?)* |
| 1.14b | **OpenTelemetry GenAI names:** span and tag names in `AgentTelemetry` follow the standard `gen_ai.*` conventions, e.g. `gen_ai.operation.name` (`chat`, `execute_tool`), `gen_ai.request.model`, `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, `gen_ai.tool.name`. This is naming only; still no packages. |
| 1.15 | **Review checkpoint.** |

**Done when:** `dotnet build eCom.sln` passes, the tests pass, and the API still starts without any agent registration.

## Phase 2 – Schema and seed

| Step | Work |
|---|---|
| 2.1 | Back up the DB again (`pg_dump`) to a permanent folder. |
| 2.2 | Add `Color`, `Material`, `StockQuantity`, `Rating` to `Product` (details in [04](04-database-and-seed.md)). **No model default for StockQuantity**; the migration sets 25 on existing rows with SQL. |
| 2.3 | Add the `AgentRuns`, `EvalRuns` and `EvalCaseResults` tables in `StoreContext`. EF maps the Agent project's record classes directly. Configs go in `Infrastructure/Data/Config/`. |
| 2.4 | Migration `AgentAndProductAttributes` for StoreContext. |
| 2.5 | Node seed generator `tools/seed-gen/generate.js`: about 1,000 products, about 20 brands, the 4 existing types, image re-use by type prefix. Output: `products.json`, `brands.json`. Deterministic (fixed random seed). |
| 2.6 | Descriptions: templates by default. The LLM option is a separate script, **run only after the owner approves the cost**. |
| 2.7 | Policy markdown files in `Infrastructure/Data/SeedData/policies/`. |
| 2.8 | Make `StoreContextSeed` idempotent: add brands, types and products that are missing (matched by name). Never delete or change existing rows. |
| 2.9 | Point `AiDocumentService` at the policy files too, but **don't sync to Azure** without approval. |
| 2.10 | **Review checkpoint.** |

**Done when:** `dotnet run --migrate` on the local DB gives about 1,000 products, the original 18 are unchanged, and running it twice adds nothing.

## Phase 3 – Tools and API

| Step | Work |
|---|---|
| 3.1 | Tools in `Infrastructure/AgentTools/` (contracts in [05 §3](05-agent-guide.md#3-tools)). |
| 3.2 | `ProductSearchSpecification` with the new filters (brand, type, colour, material, price range, in stock, min rating) and sorting (price, rating, name). Brand and type names are checked against the DB; an unknown name returns a helpful list. |
| 3.3 | `search_knowledge`: Azure retriever when configured, otherwise a local keyword search over the policy markdown files. The tool result says which one was used. |
| 3.4 | `EfAgentRunStore` / `EfEvalRunStore` in Infrastructure. |
| 3.5 | `AgentController`: `POST api/agent/chat`, `GET api/agent/runs`, `GET api/agent/runs/{id}`, `GET api/agent/stats`, `GET api/agent/info`, `POST api/agent/evals`, `GET api/agent/evals`, `GET api/agent/evals/{id}`. |
| 3.6 | A middleware/filter fills `AgentRequestContext` from the JWT (optional auth on chat). |
| 3.7 | Rate limit with the built-in `Microsoft.AspNetCore.RateLimiting` (fixed window per user, or per IP when anonymous). |
| 3.8 | `API/Agent/system-prompt.md`; config section `Agent` in `appsettings.json`. |
| 3.9 | `API/Agent/evals/cases.json`, about 45 cases, each with `expectedTools`: the 21 ported cases, ~6 multi-tool, ~6 ambiguous, ~6 should refuse / "I don't know", ~6 safety, ~3 orders (signed in vs anonymous). The multi-turn case comes in 5.9. |
| 3.10 | Swagger smoke test of every endpoint. **Review checkpoint.** |

**Phase 3 must also (found in Phase 1):**
- **`Dockerfile`:** once the API references `Agent/`, the Docker build needs `COPY Agent/Agent.csproj Agent/` before the restore and `COPY Agent/. Agent/` before the publish. The Dockerfile has the owner's uncommitted changes, so **ask the owner first**.
- **`Program.cs`:** call `EvalCommandLine.IsEvalCommand(args)` / `EvalCommandLine.RunAsync(...)` so `dotnet run --evals` works (step 1.13b built the engine side).
- **Tools** implement `ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken)`: the context is passed in, so tools don't need to inject it.

## Phase 4 – Angular 11 UI

| Step | Work |
|---|---|
| 4.1 | Lazy `AgentModule` at `/agent` with child routes `chat`, `runs`, `runs/:id`, `evals`, `how-it-works`. |
| 4.2 | `AgentService` (HTTP calls), models matching the API DTOs. |
| 4.3 | **Chat page:** messages on the left. On the right, the observability panel for the selected answer: a timeline of guardrail checks, LLM calls and tool calls (input, output, tokens, ms), plus totals (tokens, cost, latency, model) and sources. Modelled on `SimpleAgent.Api/wwwroot/index.html`. |
| 4.4 | **Runs page:** a paged table (time, question, tools, tokens, cost, ms, blocked, user) with a detail view that reuses the timeline component. Stats cards at the top. |
| 4.5 | **Evals page:** a "Run evals" button (with an option for the LLM judge), progress polling, a results table with per-check pass/fail, and a history of previous runs. |
| 4.6 | **How it works page:** the mermaid diagrams from [03](03-workflow.md) as static SVG or HTML, with short explanations. |
| 4.7 | Nav-bar link "Assistant". **Review checkpoint.** |

## Phase 5 – End to end and comparison

| Step | Work |
|---|---|
| 5.1 | Run the full stack locally (Docker DB, API, `ng serve` on 4201). |
| 5.2 | Run the evals with and without the judge; save the results. |
| 5.3 | Run the same 21 questions through the old `/api/ai/chat`; write a comparison table (pass rate, latency, cost). |
| 5.4 | Write `docs/agentic/07-results.md`. The owner decides whether to retire `AiChatService`. |
| 5.5 | **Loop-control improvements (BRD R11).** In `AgentOptions`: `MaxToolCallsPerRun` (10) and `ToolTimeoutSeconds` (10, passed to `ToolRegistry`). In `AgentRunner`: count tool calls across the run and stop with a polite message at the cap; keep a per-run dictionary of `name + sorted-args JSON` and return "You already called this; use the earlier result" for a repeat (recorded as a step); set `AgentStep.IsError` from `ToolExecutionResult.Success`. Tools stay sequential. |
| 5.6 | UI: show error steps in red and repeated calls in amber in the timeline. |
| 5.7 | Eval cases: one that forces a tool error (e.g. an unknown brand) and checks the agent recovers; one that checks a repeated call is not executed twice. Re-run the evals and add the before/after numbers to `07-results.md`. |
| 5.8 | **Shrink old tool results (BRD R12).** Each tool gets an optional `Summarise(output)` that returns a short stub keeping the ids (default: the first 200 characters). When `AgentRunner` saves the history at the end of a turn, it replaces each tool result from that turn with its stub. The current turn always sees the full results. |
| 5.9 | Eval format gains `turns` (a list of messages run in one session; checks apply to the last answer). Add the case "Where's my last order?" → "When will it arrive?" (signed in as the seeded test user with an order). Record input tokens per turn before and after 5.8 in `07-results.md`. |
| 5.10 | **Groundedness check (BRD R7, S8).** A grader pulls every price (`$12.99`, `12.99 USD`) and stock number ("3 left", "in stock: 12") out of the answer with regular expressions, and checks each value appears in that run's tool outputs (numbers compared as numbers). It reports matched / made-up counts per run, and the made-up rate in the eval summary and the run detail. No LLM call. |
| 5.11 | Final eval run with `repeat 5`; fill in all success metrics (S1–S9) in `07-results.md`; save the result as the first **baseline** (`--save-baseline`). **Review checkpoint.** |

## Phase 6 – Production rollout

Load the new data into the **cloud database first**, then fill the **live index from the cloud database**. The full runbook, with checks and rollback, is in [04 §7](04-database-and-seed.md#7-production-rollout-cloud-database-then-index).

| Step | Work |
|---|---|
| 6.1 | Back up the cloud `skinet` database (point-in-time restore time + `pg_dump` to a permanent folder). Record counts. |
| 6.2 | Owner merges to `main`; the GitHub workflow deploys the new image. |
| 6.3 | Run migrate + seed once (`RUN_MIGRATIONS=true`, 1 replica, then back to `false`). Verify counts. |
| 6.4 | Update the index schema (`INIT_SEARCH_INDEX=true` once). |
| 6.5 | Fill the indexes from the live database (`SYNC_PRODUCTS_TO_AZURE_SEARCH` / `SYNC_RAG_TO_AZURE_SEARCH` once, then back to `false`). |
| 6.6 | Smoke test the live site and the agent. Record results in `07-results.md`. |
| 6.7 | **Traces in Application Insights.** The API project (not Agent) adds the OpenTelemetry + Azure Monitor exporter packages and listens to the agent's `ActivitySource` and `Meter`. The connection string goes in a Container App secret. Check that one chat request shows as a trace: request → LLM calls → tool calls, with `gen_ai.*` attributes. Prompt and answer text is **not** exported, only metadata. *(Owner approves creating the App Insights resource; the free monthly data allowance should cover this.)* |

Every step is outward-facing, so each one runs **only when the owner says so**. Settings in the Azure portal can be changed by the owner, or by Claude through `az containerapp update` if the owner asks and is signed in.

---

## Config sketch (`appsettings.json`)

```json
"Agent": {
  "AppName": "eCOM-AI-M Shop Assistant",
  "SystemPromptPath": "Agent/system-prompt.md",
  "FallbackAnswer": "Sorry, I can't help with that. I can find products, explain delivery, returns and payments, and check your orders when you're signed in.",
  "Llm": { "Provider": "OpenAI", "Model": "gpt-5.4-mini" },
  "MaxSteps": 6,
  "MaxTokensPerMessage": 20000,
  "MaxHistoryMessages": 20,
  "MaxInputChars": 2000,
  "Temperature": null,
  "Canary": "CANARY-<random>",
  "Pricing": { "InputPerMillionUsd": 0.0, "OutputPerMillionUsd": 0.0 },
  "Evals": { "CasesPath": "Agent/evals/cases.json", "BaselinePath": "Agent/evals/baseline.json", "DefaultRepeat": 3, "JudgePassScore": 4, "MaxDropPoints": 5 },
  "RateLimit": { "PermitsPerMinute": 10 }
}
```

The LLM key is read from the existing `OpenAI:ApiKey` when `Agent:Llm:ApiKey` is empty, so there is only one secret. `Agent:Llm:Provider` defaults to `Fake` (free, offline), so a missing setting can never cost money. Leave `Canary` empty to get a random one at each start. The pricing numbers go in when this section is added to `appsettings.json` in Phase 3 (copy them from the current OpenAI price list); cost is always labelled **estimated**.

## Open technical decisions (confirm in Phase 1)

| Topic | Proposal |
|---|---|
| OpenAI API style | Keep SimpleAgent's **Chat Completions** client (tool calling is simple and well known). The existing answer generator uses the Responses API; both can live side by side. *Done this way in Phase 1; owner to confirm (D6).* |
| Test project | Add `Agent.Tests` (xUnit). *Added in Phase 1; owner to confirm (D5).* |
| Run store | EF in `StoreContext` (same DB, same migrations flow). |
