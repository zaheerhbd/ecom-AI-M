# 08 – ADR: Why build our own agent engine instead of using a framework

| | |
|---|---|
| Status | Accepted (2026-11-15) |
| Deciders | Owner |
| Related | [01 BRD](01-brd.md) · [05 Agent guide](05-agent-guide.md) |

## Context

The shop needs a tool-using agent: a loop around an LLM, with tools, guardrails, memory, evals and tracing. The .NET world already has ready-made options:

| Option | What it is |
|---|---|
| **Microsoft.Extensions.AI** | A thin set of shared interfaces (`IChatClient`, `IEmbeddingGenerator`, AI functions), plus middleware for function calling, caching, logging and telemetry. It is not a full agent framework; it's the common building block the others use. |
| **Semantic Kernel** | Microsoft's SDK for AI apps: plugins (tools), prompt templates, connectors to many models and vector stores, and automatic function calling. |
| **Microsoft Agent Framework** | Microsoft's newer agent framework, which brings Semantic Kernel and AutoGen together: agents, multi-agent workflows, threads (memory) and built-in OpenTelemetry. |

All three can run the "LLM → tool → LLM" loop for us in a few lines.

## Decision

**Build the engine by hand** (the `Agent/` project, copied from the owner's SimpleAgent learning project), with **no NuGet packages**.

## Why

1. **The goal is to learn how an agent works.** A framework hides the loop, the tool-call message format, history trimming and token counting. Writing them makes each one visible and explainable.
2. **Full visibility.** Every guardrail check, LLM call and tool call becomes a step we store and show in the UI. With a framework we would have to dig these out of filters and callbacks.
3. **Small and readable.** About 1,500 lines of C# 7.3 with plain-English comments, which one person can read in an afternoon.
4. **No version churn.** These frameworks change quickly; a hand-written engine only changes when we change it.
5. **A clear comparison afterwards.** Once you've built the loop yourself, you can tell exactly what a framework gives you and what it costs.

## What we give up

| We lose | Impact here |
|---|---|
| Ready connectors for many models and vector stores | Low: we use one provider (OpenAI) and one search service |
| Automatic function calling, retries, middleware | Low: our loop is ~150 lines, and tools are wrapped in a timeout and error handler |
| Multi-agent workflows | None: one agent is enough for a shop assistant |
| Community support and documented patterns | Medium: mitigated by the docs in this folder |
| Built-in OpenTelemetry | Low: we use `ActivitySource`/`Meter` with the standard `gen_ai.*` names (Phase 1) and export to App Insights (Phase 6) |

## How our parts map to the frameworks

| Our engine | Microsoft.Extensions.AI | Semantic Kernel | Agent Framework |
|---|---|---|---|
| `ILlmClient` / `OpenAiLlmClient` | `IChatClient` | Chat completion service | Chat client |
| `ITool` + `ToolRegistry` | `AIFunction` / `AIFunctionFactory` | Plugin + `[KernelFunction]` | Function tools |
| `AgentRunner` loop | `FunctionInvokingChatClient` | Auto function calling | `ChatClientAgent` run |
| `IConversationStore` | (you keep the message list) | `ChatHistory` | Agent thread |
| Guardrails | Middleware (`DelegatingChatClient`) | Filters | Middleware |
| `AgentTelemetry` (`ActivitySource`) | `UseOpenTelemetry()` | Built-in telemetry | Built-in OpenTelemetry |
| Eval runner | Microsoft.Extensions.AI.Evaluation | – | – |

## When we would switch to a framework

- Production work in a team that already knows it, or where Microsoft support matters.
- Many models or vector stores, or switching between them often.
- Several agents working together (planner + workers, handoffs).
- When the hand-written engine starts re-building features the framework already has.

**Easiest first step:** make `OpenAiLlmClient` implement or wrap `IChatClient`. That lets us use the Microsoft.Extensions.AI middleware without changing the loop.

## How to answer "Why build your own?" in an interview

> "To learn the loop properly. I built the agent by hand, so I know exactly what happens on each step: the tool-call format, history trimming, token budgets, guardrails and tracing. Then I compared it with Semantic Kernel, Agent Framework and Microsoft.Extensions.AI and mapped each part to its framework equivalent. In a production team I'd usually start from Microsoft.Extensions.AI or Agent Framework, and I can tell you what they'd give us and what they'd hide."
