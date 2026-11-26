# 10 – Phase 7 plan: Microsoft Agent Framework as a second engine

> **Status:** **built 2026-11-26**, waiting for the owner's code review (not committed). Report: [reports/phase-7-report.html](reports/phase-7-report.html).
> **Written:** 2026-11-25 (plan) · the owner OK'd D10–D12 and the Dockerfile lines with "ok let's do it as part of phase 7".
>
> **Built differently from this plan, for the better** (details in the report, "Changes from the plan"):
> - No `IAgentEngine`. Instead the **loop** is pluggable inside `AgentRunner` (`Agent/IAgentLoop.cs`: `IAgentLoop` + `AgentLoopRun`), so the outer steps exist once and the controller/eval runner are unchanged.
> - Only **`Microsoft.Agents.AI`** (no `.OpenAI`, no OpenAI SDK): the framework calls the model through our own `ILlmClient` (`LlmChatClient`), which removes the Gemini thought-signature risk (7.3) and keeps the Fake model for free tests.
> - Framework `MaximumIterationsPerRequest = MaxSteps − 1` (it counts tool rounds, then adds one tool-less call), so the cap on AI calls matches; on that last call the AI answers instead of the custom loop's apology.
> - 7.8 (full comparison run) not done: needs the owner's OK (paid or many days of free Gemini).

## 1. Goal (short)

Add **Microsoft Agent Framework** as a second way to run the shop assistant, next to our own hand-written engine.

- **One setting picks the engine:** `Agent:Engine` = `Custom` (default, today's engine) or `AgentFramework`.
- **The API stays exactly the same.** Same endpoints (`api/agent/*`), same request, same JSON answer (`AgentResult`), same run records, same eval pages. The Angular UI needs no changes.
- **Everything around the loop is shared:** the 7 tools, guardrails, memory, the session-owner check, the groundedness check, run records, evals and the rate limit.
- **Only the loop is swapped.** With `AgentFramework`, Microsoft's `ChatClientAgent` decides when to call tools, instead of our `AgentRunner` loop.

Why: we can run the same 56 eval cases on both engines and compare them (speed, cost, tool choice, made-up facts). This is the "When we would switch to a framework" path from [08-adr-why-build.md](08-adr-why-build.md).

## 2. What we use

| Package | Version (NuGet, 2026-11-25) | What for |
|---|---|---|
| `Microsoft.Agents.AI` | 1.28.0 (stable) | `ChatClientAgent`, `AIAgentBuilder` (middleware), sessions, tool-result compaction |
| `Microsoft.Agents.AI.OpenAI` | 1.28.0 | Turns an OpenAI-compatible chat client into an agent |
| `Microsoft.Extensions.AI` (+ `.OpenAI`) | 10.11.x (comes with the above) | `IChatClient`, `AIFunction`, function calling, OpenTelemetry |

All target **net10.0**, the same as the app.

## 3. How it fits together

```
AgentController / EvalRunner
        |
   IAgentEngine            <- NEW small interface: RunAsync(sessionId, message) -> AgentResult
     /        \
AgentRunner   FrameworkAgentEngine        <- NEW, in a new project "Agent.Framework"
 (today)        |
                |  shared, unchanged: guardrails, memory store, owner check, groundedness,
                |  run store, telemetry, AgentResult, ToolRegistry (sign-in rule + timeouts)
                |
            ChatClientAgent (Microsoft)
                |  tools  = our 7 ITool, wrapped as AIFunction (ToolFunctionAdapter)
                |  model  = IChatClient for the same provider settings (Gemini / OpenAI / Azure OpenAI)
                |  steps  = a small IChatClient wrapper records each LLM call + tokens for the timeline
```

The **Agent** project keeps its rules (no NuGet, C# 7.3). All framework code goes in the new **`Agent.Framework`** project, so the hand-written engine stays exactly as it is.

## 4. Steps

| Step | Work | Notes |
|---|---|---|
| 7.1 | Add `IAgentEngine` to the Agent project. `AgentRunner` implements it (no logic change). `AgentController` and `EvalRunner` ask for `IAgentEngine` instead of `AgentRunner`. Add `Agent:Engine` (default `Custom`). | Pure refactor. All 57 tests must still pass |
| 7.2 | New project `Agent.Framework` (net10.0, references Agent only, never Core/Infrastructure/API). Add it to `eCom.sln`; API references it. | Packages from §2, versions pinned |
| 7.3 | **Model client:** build an `IChatClient` from the same `LlmOptions` (key, base URL, model) for Gemini, OpenAI and Azure OpenAI. | **Risk:** Gemini 3 needs its "thought signature" sent back on tool calls (our own client does this by hand). Test this first; if the standard client drops it, the framework engine runs on OpenAI / Azure OpenAI only and says so at startup |
| 7.4 | **Tools:** `ToolFunctionAdapter` wraps each `ITool` as an `AIFunction` (same name, description, JSON schema). Running it goes through our `ToolRegistry`, so the sign-in rule and the tool timeout still apply. Anonymous users only get the tools they may see. | Loop control lives here too: repeated identical calls are not run again, and `MaxToolCallsPerRun` is enforced, both with the same messages as today |
| 7.5 | **`FrameworkAgentEngine`:** the same outer steps as `AgentRunner` (input guardrails → owner check → load memory → *run the framework agent* → output guardrails → groundedness → shrink + save memory → save run record). Map `MaxSteps` to the framework's maximum tool-call rounds. Turn the framework's answer (function calls, results, usage) into our `AgentStep`s, tokens and cost. Provider errors (429, bad key) become the same safe messages as `LlmException`. | Memory stays in **our** store (one format for both engines), so a chat can even switch engine between messages |
| 7.6 | **Run records + info:** the engine name goes into the existing `Model` field, e.g. `gemini-3.5-flash · Agent Framework`. | **No new DB column, no migration**, so nothing changes in the cloud DB. The Runs and Evals pages show it in the existing model column |
| 7.7 | **Tests (free):** a fake `IChatClient` drives the framework engine: tool call → answer; anonymous user can't use `get_my_orders`; repeat detection; tool-call limit; guardrail block (model never called); `AgentResult` has the same shape as the custom engine's. | Existing 57 tests unchanged |
| 7.8 | **Compare:** run a few eval cases on both engines locally. Full 56 × 3 on both only with the owner's OK (cost below). Write results into [07-results.md](07-results.md). | |
| 7.9 | Report `reports/phase-7-report.html`, tracker row, HANDOFF. **Code review checkpoint** (rule 2). | |

**Not in this phase:** deploying the framework engine to production. Production stays `Custom` until the owner sets `Agent__Engine=AgentFramework` on the Container App.

## 5. Configuration

```jsonc
"Agent": {
  "Engine": "Custom",          // or "AgentFramework"
  "Llm": { "Provider": "Gemini" }   // unchanged: Gemini / OpenAI / AzureOpenAI, used by BOTH engines
}
```

In Azure: `Agent__Engine=AgentFramework`. Any other value fails at startup with a clear message (same style as the provider check).

## 6. What changes, file by file (expected)

| Where | Change |
|---|---|
| `Agent/IAgentEngine.cs` (new), `Agent/AgentRunner.cs`, `Agent/AgentOptions.cs`, `Agent/ServiceCollectionExtensions.cs`, `Agent/Evals/EvalRunner.cs` | Interface + `Engine` option + DI registration; no loop changes |
| `Agent.Framework/` (new project) | `FrameworkAgentEngine`, `ToolFunctionAdapter`, `ChatClientFactory`, `StepRecordingChatClient`, `MessageMapper`, `AddAgentFrameworkEngine()` |
| `Agent.Tests/` or new `Agent.Framework.Tests/` | Tests from 7.7 |
| `API/API.csproj`, `API/Extensions/AgentServiceExtensions.cs`, `API/Controllers/AgentController.cs` | Project reference, engine choice, `IAgentEngine` |
| `eCom.sln` | New project(s) |
| `Dockerfile` | **2 COPY lines** for the new project (the owner's file, so ask first, like Phase 2) |
| `docs/agentic/*` | This plan, tracker, HANDOFF, results, report |

No changes to: the Angular client, the database, Core, Infrastructure (tools stay as they are), the system prompt, eval cases.

## 7. Decisions for the owner

| # | Question | Recommendation |
|---|---|---|
| D10 | C# style in `Agent.Framework` | **Try C# 7.3** like the rest. If a framework type needs newer syntax, raise it **only in that project** and say so in the review |
| D11 | Where the engine name is shown | **Inside the `Model` field** (no migration, API unchanged). A separate `Engine` column would need a migration on the cloud DB |
| D12 | LLM for the framework engine | Same provider as the custom engine (Gemini free today). If the Gemini thought-signature check in 7.3 fails, use OpenAI or Azure OpenAI for this engine |

## 8. Cost and risks

- **Building and tests:** free (fake model). A few manual chats on Gemini free count toward the 20 requests/day.
- **Comparison run:** 56 cases × 3 × 2 engines on OpenAI ≈ **$0.80–1.50**. Only with the owner's OK.
- **Gemini + framework** (7.3 risk above): tested first, so we know early.
- **Small behaviour differences** between the two loops (the order of tool calls, retries) are expected. That is what the comparison measures.
- **Package churn:** the framework releases often. Versions are pinned; upgrade on purpose.

## 9. Done when

- `Agent:Engine=AgentFramework` answers through `POST api/agent/chat` with the same JSON shape, the timeline shows the steps, and runs/evals are saved and visible on the existing pages.
- `Agent:Engine=Custom` (default) behaves exactly as before: 57/57 tests, same answers.
- New framework tests pass; a small eval runs on both engines; results and the report are written.
