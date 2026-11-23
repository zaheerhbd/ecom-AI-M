// Shapes of the JSON returned by api/agent/* (see Agent/AgentResult.cs, Agent/Runs, Agent/Evals).
// Property names are camelCase because ASP.NET Core serialises that way.

/** One thing the agent did while answering: a guardrail stage, an LLM call or a tool call. */
export interface IAgentStep {
  index: number;
  type: 'guardrail' | 'llm' | 'tool' | string;
  name: string;
  input: string | null;
  output: string | null;
  inputTokens: number;
  outputTokens: number;
  durationMs: number;
  isError: boolean;
  /** The same tool + arguments was asked for again in this answer, so it was not run again. */
  isRepeat: boolean;
}

/** GuardrailOutcome in C# is an enum sent as a number. */
export enum GuardrailOutcome {
  Passed = 0,
  Modified = 1,
  Blocked = 2,
  Skipped = 3
}

export interface IGuardrailCheck {
  stage: 'input' | 'output' | string;
  name: string;
  outcome: GuardrailOutcome;
  reason: string | null;
  durationMs: number;
}

/** Something a tool looked at (a product, a policy, an order), shown with the answer. */
export interface IAgentSource {
  type: string;
  id: string;
  title: string;
}

/** The fields the chat answer and a saved run have in common. The timeline shows these. */
export interface IAgentTrace {
  answer: string;
  blocked: boolean;
  blockedBy: string | null;
  stopReason: string | null;
  error: string | null;
  model: string;
  inputTokens: number;
  outputTokens: number;
  estimatedCostUsd: number;
  durationMs: number;
  toolsUsed: string[];
  steps: IAgentStep[];
  guardrailChecks: IGuardrailCheck[];
  sources: IAgentSource[];
  /** Groundedness: prices / stock numbers in the answer backed by tool data. */
  groundedFactCount: number;
  /** Prices / stock numbers no tool result backs up, e.g. "$19.99". */
  madeUpFacts: string[];
}

/** POST api/agent/chat */
export interface IAgentResult extends IAgentTrace {
  runId: string;
  sessionId: string;
  question: string;
  blockedReason: string | null;
  traceId: string;
}

/** One saved run (GET api/agent/runs, runs/{id}). */
export interface IAgentRun extends IAgentTrace {
  id: string;
  createdAt: string;
  sessionId: string;
  userEmail: string | null;
  channel: string;
  question: string;
  evalRunId: string | null;
}

export interface IPage<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface IToolUsage {
  name: string;
  count: number;
}

/** GET api/agent/stats */
export interface IAgentStats {
  totalRuns: number;
  blockedRuns: number;
  errorRuns: number;
  blockRate: number;
  averageDurationMs: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalEstimatedCostUsd: number;
  topTools: IToolUsage[];
}

/** GET api/agent/info */
export interface IAgentInfo {
  appName: string;
  model: string;
  maxSteps: number;
  maxHistoryMessages: number;
  maxInputChars: number;
  rateLimitPerMinute: number;
  runRetentionDays: number;
  tools: { name: string; description: string; requiresSignIn: boolean }[];
}

/** POST api/agent/evals body. Leave fields out to use the server defaults. */
export interface IEvalRequest {
  caseIds?: string[];
  category?: string;
  repeat?: number;
  useJudge?: boolean;
}

export interface IEvalRun {
  id: string;
  startedAt: string;
  finishedAt: string | null;
  status: 'Running' | 'Completed' | 'Failed' | string;
  error: string | null;
  useJudge: boolean;
  repeat: number;
  model: string;
  totalCases: number;
  completedCases: number;
  passedCases: number;
  flakyCases: number;
  totalAttempts: number;
  passedAttempts: number;
  passRate: number;
  correctToolRate: number;
  factsChecked: number;
  madeUpFacts: number;
  /** 0..1, null when no answer had a price or stock number. */
  madeUpRate: number | null;
  medianLatencyMs: number;
  p95LatencyMs: number;
  medianTokens: number;
  totalCostUsd: number;
}

export interface IEvalCheck {
  name: string;
  passed: boolean;
  detail: string;
}

export interface IEvalCaseResult {
  id: number;
  evalRunId: string;
  caseId: string;
  category: string;
  input: string;
  passed: boolean;
  flaky: boolean;
  repeatCount: number;
  passCount: number;
  checks: IEvalCheck[];
  answer: string;
  judgeScore: number | null;
  agentRunId: string | null;
  durationMs: number;
  tokens: number;
}

/** GET api/agent/evals/{id} */
export interface IEvalRunDetail {
  run: IEvalRun;
  results: IEvalCaseResult[];
}

/** A message shown in the chat window. */
export interface IChatMessage {
  role: 'user' | 'assistant' | 'notice';
  text: string;
  result?: IAgentResult;
}
