import { Component, Input, OnChanges } from '@angular/core';
import { GuardrailOutcome, IAgentStep, IAgentTrace, IGuardrailCheck } from '../agent.models';

/**
 * Shows HOW an answer was made: totals, every guardrail check, every LLM and tool step
 * (click a step to see what went in and out), and the sources the tools used.
 * Used by the chat page and the run detail page.
 */
@Component({
  selector: 'app-agent-timeline',
  templateUrl: './agent-timeline.component.html',
  styleUrls: ['./agent-timeline.component.scss']
})
export class AgentTimelineComponent implements OnChanges {
  @Input() trace: IAgentTrace;

  inputChecks: IGuardrailCheck[] = [];
  outputChecks: IGuardrailCheck[] = [];
  llmCalls = 0;
  open = new Set<number>();

  ngOnChanges(): void {
    const checks = (this.trace && this.trace.guardrailChecks) || [];
    this.inputChecks = checks.filter(c => c.stage === 'input');
    this.outputChecks = checks.filter(c => c.stage === 'output');
    this.llmCalls = ((this.trace && this.trace.steps) || []).filter(s => s.type === 'llm').length;
    this.open.clear();
  }

  toggle(step: IAgentStep): void {
    if (this.open.has(step.index)) {
      this.open.delete(step.index);
    } else {
      this.open.add(step.index);
    }
  }

  canOpen(step: IAgentStep): boolean {
    return !!(step.input || step.output);
  }

  outcomeLabel(check: IGuardrailCheck): string {
    return GuardrailOutcome[check.outcome] || String(check.outcome);
  }

  outcomeClass(check: IGuardrailCheck): string {
    switch (check.outcome) {
      case GuardrailOutcome.Passed: return 'chip-good';
      case GuardrailOutcome.Modified: return 'chip-warn';
      case GuardrailOutcome.Blocked: return 'chip-bad';
      default: return 'chip-grey';
    }
  }

  stepIcon(step: IAgentStep): string {
    switch (step.type) {
      case 'guardrail': return 'fa-shield';
      case 'llm': return 'fa-commenting-o';
      case 'tool': return 'fa-wrench';
      default: return 'fa-circle-o';
    }
  }

  stepLabel(step: IAgentStep): string {
    switch (step.type) {
      case 'guardrail': return 'Safety check';
      case 'llm': return 'AI model';
      case 'tool': return 'Tool';
      default: return step.type;
    }
  }

  /** Prices / stock numbers no tool result backs up (older runs may not have the field). */
  get madeUp(): string[] {
    return (this.trace && this.trace.madeUpFacts) || [];
  }

  /** All prices / stock numbers that were checked. */
  get factCount(): number {
    return ((this.trace && this.trace.groundedFactCount) || 0) + this.madeUp.length;
  }

  stopReasonText(reason: string): string {
    switch (reason) {
      case 'max-steps': return 'Stopped: too many steps';
      case 'token-budget': return 'Stopped: token budget used up';
      case 'max-tool-calls': return 'Stopped: too many tool calls in one answer';
      default: return 'Stopped: ' + reason;
    }
  }
}
