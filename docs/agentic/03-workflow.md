# 03 – Workflow Diagrams

These diagrams use Mermaid. VS Code shows them with a Mermaid preview extension, and GitHub renders them directly. The "How it works" page in Phase 4 reuses them.

## 1. Old flow vs new flow

```mermaid
flowchart LR
  subgraph OLD["Old: fixed pipeline (AiChatService)"]
    direction TB
    o1[Question] --> o2[Keyword intent<br/>product or policy]
    o2 --> o3[One vector search<br/>Azure rag-index]
    o3 --> o4{Score gate}
    o4 -->|good| o5[LLM writes answer]
    o4 -->|weak| o6[Template reply]
  end
  subgraph NEW["New: agent loop (AgentRunner)"]
    direction TB
    n1[Question] --> n2[Input guardrails]
    n2 --> n3[LLM + tool list]
    n3 -->|wants tool| n4[Run tool<br/>SQL / policy / orders]
    n4 --> n3
    n3 -->|final answer| n5[Output guardrails]
    n5 --> n6[Answer + trace + cost]
  end
```

## 2. Request through the layers

```mermaid
flowchart TB
  UI[Angular /agent/chat] -->|POST api/agent/chat<br/>+ optional JWT| RL[Rate limiter]
  RL --> CTRL[AgentController]
  CTRL --> CTX[Fill AgentRequestContext<br/>email from JWT, channel]
  CTX --> RUN[AgentRunner<br/>scoped]
  RUN --> GIN[Input guardrails]
  RUN --> LLM[ILlmClient<br/>OpenAI]
  RUN --> REG[ToolRegistry<br/>hides sign-in tools when anonymous]
  REG --> T1[search_products] --> DB[(PostgreSQL skinet)]
  REG --> T2[get_product_details] --> DB
  REG --> T3[list_catalog_filters] --> DB
  REG --> T4[search_knowledge] --> AZ[(Azure AI Search)]
  T4 -.fallback.-> MD[policies/*.md]
  REG --> T5[get_delivery_options] --> DB
  REG --> T6[get_my_orders / get_order] --> DB
  RUN --> GOUT[Output guardrails]
  RUN --> STORE[IAgentRunStore] --> DB
  RUN -->|AgentResult| CTRL --> UI
```

## 3. The agent loop in detail

```mermaid
sequenceDiagram
  autonumber
  participant U as User
  participant R as AgentRunner
  participant G as Guardrails
  participant L as LLM
  participant T as Tool
  participant S as RunStore

  U->>R: "Cheapest waterproof gloves and how fast can they arrive?"
  R->>G: input checks (length, injection, PII)
  G-->>R: passed (PII modified if found)
  loop up to MaxSteps, within token budget
    R->>L: system prompt + history + tool definitions
    alt model asks for tools
      L-->>R: tool_calls: search_products{type:Gloves, sort:priceAsc}, get_delivery_options{}
      R->>T: execute each call (typed args, user from context)
      T-->>R: JSON result (+ sources)
    else model gives final answer
      L-->>R: text
    end
  end
  R->>G: output checks (canary leak, PII)
  G-->>R: passed / blocked
  R->>S: save run (steps, checks, tokens, cost, ms)
  R-->>U: answer + steps + sources + cost
```

## 4. Guardrail decisions

```mermaid
flowchart TB
  A[Text] --> L{Too long?}
  L -->|yes| B1[BLOCKED: InputLength]
  L -->|no| I{Injection pattern?<br/>ignore previous / reveal prompt}
  I -->|yes| B2[BLOCKED: PromptInjection]
  I -->|no| P{Email / phone / card?}
  P -->|yes| M[MODIFIED: redacted text goes on]
  P -->|no| OK[PASSED]
  M --> LLM[to the LLM]
  OK --> LLM
  B1 --> F[Fallback answer, LLM never called]
  B2 --> F
```

Output side: `SecretLeak` blocks when the answer contains the canary, and `PiiRedaction` masks personal data. A guardrail that does not apply (for example the output checks after an input block) is recorded as **Skipped**.

## 5. Sign-in gate for order tools

```mermaid
flowchart LR
  Q[Request] --> J{Valid JWT?}
  J -->|yes| C1[Context.UserEmail = email claim]
  J -->|no| C2[Context.UserEmail = null]
  C1 --> D1[Tool list includes get_my_orders, get_order]
  C2 --> D2[Tool list hides them]
  D1 --> X[get_order id=7]
  X --> W{Order.BuyerEmail == Context.UserEmail?}
  W -->|yes| R1[Order details]
  W -->|no| R2[Not found<br/>same reply as a missing order]
```

The model never supplies the email. Even if it puts `email` in the arguments, the tool ignores it.

## 6. Eval run

```mermaid
sequenceDiagram
  participant UI as Evals page
  participant API as AgentController
  participant ER as EvalRunner (background)
  participant AR as AgentRunner (new scope per case)
  participant DB as EvalRuns / EvalCaseResults

  UI->>API: POST api/agent/evals {judge: true}
  API->>DB: insert EvalRun (status Running)
  API-->>UI: 202 {evalRunId}
  API->>ER: start in background
  loop each case in cases.json
    ER->>AR: RunAsync(case.input, case.signedInAs)
    AR-->>ER: AgentResult
    ER->>ER: grade (tool, args, mustContain, shouldBlock, judge)
    ER->>DB: insert EvalCaseResult, update progress
  end
  ER->>DB: EvalRun status Completed, pass rate
  UI->>API: GET api/agent/evals/{id} (polling)
  API-->>UI: progress + results
```

## 7. Data flow for seeding (Phase 2)

```mermaid
flowchart LR
  G[tools/seed-gen/generate.js<br/>fixed random seed] --> P[products.json ~1,000]
  G --> B[brands.json ~20]
  LLMD[Optional: LLM descriptions<br/>owner approves cost] -.-> P
  POL[policies/*.md] --> AD[AiDocumentService]
  P --> SEED[StoreContextSeed<br/>adds missing rows only]
  B --> SEED
  SEED --> DB[(skinet)]
  DB --> AD
  AD -.only with approval.-> AZ[(Azure rag-index)]
```
