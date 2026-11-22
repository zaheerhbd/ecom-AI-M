import { Component, OnDestroy, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { interval, Subscription } from 'rxjs';
import { switchMap, takeWhile } from 'rxjs/operators';
import { describeAdminError } from '../agent-access';
import { IEvalCaseResult, IEvalRequest, IEvalRun } from '../agent.models';
import { AgentService } from '../agent.service';

type EvalMode = 'quick' | 'category' | 'cases' | 'all';

/**
 * Start an eval (automatic test) run, watch it finish, and read the result of every case.
 * Real runs call the AI for every case, so the page starts with a SMALL selection.
 */
@Component({
  selector: 'app-agent-evals',
  templateUrl: './agent-evals.component.html',
  styleUrls: ['./agent-evals.component.scss']
})
export class AgentEvalsComponent implements OnInit, OnDestroy {
  /** One product, one policy and one safety case: a cheap smoke test. */
  readonly quickCases = ['PS-001', 'TC-018', 'SF-001'];
  readonly categories = ['Product search', 'Policy', 'Delivery', 'Multi-tool', 'Ambiguous', 'Should decline', 'Safety', 'Orders'];
  readonly totalCaseCount = 51;

  mode: EvalMode = 'quick';
  category = 'Safety';
  caseIdsText = '';
  repeat = 1;
  useJudge = false;
  confirmAll = false;

  history: IEvalRun[] = [];
  current: IEvalRun | null = null;
  results: IEvalCaseResult[] = [];
  openCase = new Set<number>();

  starting = false;
  errorMessage: string | null = null;
  formMessage: string | null = null;

  private poll: Subscription;

  constructor(private agentService: AgentService) { }

  ngOnInit(): void {
    this.loadHistory(true);
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  get isRunning(): boolean {
    return !!this.current && this.current.status === 'Running';
  }

  get progress(): number {
    if (!this.current || this.current.totalCases === 0) {
      return 0;
    }
    return Math.round(100 * this.current.completedCases / this.current.totalCases);
  }

  /** How many AI conversations the chosen settings will start (shown before running). */
  get plannedConversations(): number | null {
    let cases: number | null;
    switch (this.mode) {
      case 'quick': cases = this.quickCases.length; break;
      case 'cases': cases = this.parseCaseIds().length; break;
      case 'all': cases = this.totalCaseCount; break;
      default: cases = null; // depends on the category
    }
    return cases === null ? null : cases * this.repeat;
  }

  start(): void {
    this.formMessage = null;
    const request: IEvalRequest = { repeat: this.repeat, useJudge: this.useJudge };

    if (this.mode === 'quick') {
      request.caseIds = this.quickCases;
    } else if (this.mode === 'category') {
      request.category = this.category;
    } else if (this.mode === 'cases') {
      request.caseIds = this.parseCaseIds();
      if (request.caseIds.length === 0) {
        this.formMessage = 'Type at least one case id, e.g. TC-001, SF-002.';
        return;
      }
    } else if (!this.confirmAll) {
      this.formMessage = 'Tick the box to confirm you want to run every case.';
      return;
    }

    this.starting = true;
    this.agentService.startEval(request).subscribe(run => {
      this.starting = false;
      this.confirmAll = false;
      this.show(run);
      this.loadHistory(false);
    }, (error: HttpErrorResponse) => {
      this.starting = false;
      this.formMessage = error.status === 409
        ? 'A test run is already going. Wait for it to finish.'
        : describeAdminError(error);
    });
  }

  /** Shows a run from the history (or the one just started), polling while it is still running. */
  show(run: IEvalRun): void {
    this.stopPolling();
    this.current = run;
    this.results = [];
    this.openCase.clear();
    this.refresh(run.id);

    if (run.status === 'Running') {
      this.poll = interval(3000).pipe(
        switchMap(() => this.agentService.getEval(run.id)),
        takeWhile(detail => detail.run.status === 'Running', true)
      ).subscribe(detail => {
        this.current = detail.run;
        this.results = detail.results;
        if (detail.run.status !== 'Running') {
          this.loadHistory(false);
        }
      }, (error: HttpErrorResponse) => this.errorMessage = describeAdminError(error));
    }
  }

  toggleCase(result: IEvalCaseResult): void {
    if (this.openCase.has(result.id)) {
      this.openCase.delete(result.id);
    } else {
      this.openCase.add(result.id);
    }
  }

  failedChecks(result: IEvalCaseResult): number {
    return result.checks.filter(c => !c.passed).length;
  }

  private refresh(id: string): void {
    this.agentService.getEval(id).subscribe(detail => {
      this.current = detail.run;
      this.results = detail.results;
    }, (error: HttpErrorResponse) => this.errorMessage = describeAdminError(error));
  }

  private loadHistory(showLatest: boolean): void {
    this.agentService.getEvals(1, 10).subscribe(page => {
      this.history = page.items;
      if (showLatest && !this.current && page.items.length > 0) {
        this.show(page.items[0]);
      }
    }, (error: HttpErrorResponse) => this.errorMessage = describeAdminError(error));
  }

  private parseCaseIds(): string[] {
    return this.caseIdsText
      .split(/[\s,;]+/)
      .map(id => id.trim().toUpperCase())
      .filter(id => id.length > 0);
  }

  private stopPolling(): void {
    if (this.poll) {
      this.poll.unsubscribe();
      this.poll = null;
    }
  }
}
