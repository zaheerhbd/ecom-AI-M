# 07 – Results (Phase 5)

> Written 2026-11-22. Status: the Phase 5 **code** is complete and unit-tested. The **official measured runs** (eval ×5 baseline, old-chat comparison) were **not run**, by the owner's decision: Gemini's free tier ran out, and paid OpenAI runs were declined for now. This file says what is measured, what is not, and how to finish the numbers later.

## 1. What Phase 5 added

| Step | What | Where | Status |
|---|---|---|---|
| 5.5 | Loop control (R11): `MaxToolCallsPerRun` (10), `ToolTimeoutSeconds` (10), repeat-call detection, error steps | `Agent/AgentOptions.cs`, `Agent/AgentRunner.cs`, `Agent/ServiceCollectionExtensions.cs` | ✅ built + tested |
| 5.6 | UI: repeated calls in amber, error steps in red, groundedness note, "too many tool calls" stop reason, made-up rate on the Evals page | `client/src/app/agent/agent-timeline/`, `agent-evals/` | ✅ built (`ng build` clean); not seen live (quota) |
| 5.7 | Eval cases: tool error recovery (`LC-001`), repeated call not run twice (`LC-002`, uses new `maxToolCalls`) | `API/Agent/evals/cases.json` | ✅ written; not run live |
| 5.8 | Shrink old tool results (R12): stubs that keep the ids; `ISummarisingTool` for custom stubs; `ShrinkOldToolResults` switch | `Agent/Tools/ToolResultSummariser.cs`, `AgentRunner` | ✅ built + tested + measured (Fake model) |
| 5.9 | Multi-turn eval format (`turns`) + `MM-001` "Where's my last order?" → "When will it arrive?", `MM-002`; test order for bob | `Agent/Evals/EvalCase.cs`, `EvalRunner.cs`, `docs/agentic/dev/seed-test-order.sql` | ✅ built + tested; not run live |
| 5.10 | Groundedness check (S8): prices/stock numbers must appear in tool data; made-up rate per run and per eval | `Agent/Evals/Groundedness.cs`, `Graders.cs`, `EvalRunner.cs`, DB columns (migration `AgentGroundedness`) | ✅ built + tested |
| 5.2 / 5.11 | Evals with/without judge; final eval ×5; `--save-baseline` | — | ⏸ **not run** (see §4) |
| 5.3 / 5.4 | Old `/api/ai/chat` comparison; decision on retiring `AiChatService` | — | ⏭ **skipped** by the owner (it needs paid OpenAI + the live Azure index) |

Eval cases: **51 → 56** (`OR-004`, `MM-001`, `MM-002`, `LC-001`, `LC-002`).

## 2. Measured

### Unit tests (free, deterministic)

`dotnet test Agent.Tests`: **57 / 57 pass** (43 before + 14 new in `LoopControlAndMemoryTests.cs`). They prove:

- an identical tool call (same tool, same arguments, ignoring key order, spacing and case) is **not run twice**; the step is marked `IsRepeat` and the model is told to use the earlier result;
- different arguments are not a repeat;
- `MaxToolCallsPerRun` stops the loop (stop reason `max-tool-calls`), every requested call still gets a result, and the **next message in the same chat still works**;
- `ToolTimeoutSeconds` reaches the `ToolRegistry`;
- old tool results become stubs like `[long_list: 3 items, ids 11, 22, 33. …]` for the next question, while the current answer sees the full result; `ShrinkOldToolResults: false` keeps everything;
- groundedness: `$12` matches `12.00` in a tool result, `$19.99` that is in no tool result is reported as made up, numbers the **user** typed ("under $50") count as grounded;
- a multi-turn case runs in one session and is graded on the last answer.

One older test changed: `MaxSteps_StopsAModelThatNeverFinishes` used the *same* call 10 times. With repeat detection, those are no longer run, so the test now uses different arguments per call (it still checks the step limit).

### Token saving from shrinking (R12), measured with the free Fake model

Same 3-message chat (`search_products` → "thanks" → "and what about delivery"), input tokens per message (Fake model counts ≈ 4 characters per token):

| | Message 1 | Message 2 | Message 3 |
|---|---|---|---|
| Shrinking **off** | 1,548 | 1,110 | 1,152 |
| Shrinking **on** | 1,548 | **928** | **970** |
| Saved | 0 (current answer always sees everything) | **182 (16%)** | **182 (16%)** |

A 5-product search result is about 835 characters (~210 tokens); its stub is ~27 tokens. Every later message in the chat saves that difference, **per earlier search**. The Fake model copies the whole tool result into its answer, which is also kept in history, so with a real model (short answers) the share saved is larger. Real-model numbers: run `MM-002` with the setting on and off (the eval log prints "input tokens per turn").

### Local checks

- Migration `AgentGroundedness` applied to the **local** `skinet` DB (new columns on `AgentRuns` and `EvalRuns`); runs save `GroundedFactCount` / `MadeUpFacts`.
- API answer JSON now has `groundedFactCount`, `madeUpFacts`, and `isRepeat` on each step.
- `dotnet build eCom.sln`: 0 errors (5 old warnings). `ng build`: 0 errors.
- Local test order for `bob@test.com` (order 1: Angular Speedster Board 2000 + Green React Woolen Hat, UPS2 "2-5 Days", $208 + $5.99).

## 3. Found during Phase 5

- **Gemini's free tier is 20 requests per day per model** for `gemini-3.5-flash` (it resolves to `gemini-3.6-flash`). One test question needs 2–3 requests, so the free tier covers about **7–10 questions a day**. Earlier sessions used today's quota; the first live check of the new cases failed with HTTP 429 `RESOURCE_EXHAUSTED` (`GenerateRequestsPerDayPerProjectPerModel-FreeTier`), retry after ~9.5 h. **Free Gemini is fine for trying the chat, not for evals or production.**
- The agent handled the 429 safely: polite "something went wrong" answer, `Error = LlmException` saved, eval run marked Completed with 0 passes.

## 4. Not measured yet (success metrics S1–S9)

These need a real model run. Nothing below is a result yet.

| Metric | Target (BRD) | Status |
|---|---|---|
| S1–S7 (pass rate, correct-tool rate, latency, cost, blocks…) | see [01-brd.md](01-brd.md) | Earlier spot checks only: quick check 3/3 (Phase 4), 2–3 case runs in Phase 3 |
| S8 groundedness (made-up rate ≤ 2%) | ≤ 2% | Grader built and tested; **no live rate yet** |
| S9 | see BRD | not measured |
| Baseline file (`Agent/evals/baseline.json`) | saved | **not saved** |

### How to finish the numbers (when the owner agrees)

```powershell
# OpenAI (≈ $0.15 per full pass of 56 cases, ≈ $0.75 for ×5). Set Agent:Llm:Provider = "OpenAI" first.
cd API
dotnet run -- --evals --repeat 5 --save-baseline
# or with Gemini on a paid tier / tomorrow's free quota: start small from the Evals tab (quick check).
```

Then fill §4 and record the before/after tokens for `MM-002`.

## 5. Old AI-Lab chat comparison

Skipped by the owner (2026-11-22). The old chat (`/api/ai/chat`, `AiChatService`) stays as it is. **No decision on retiring it**; revisit after Phase 6, when the live index has been refilled.
