import { Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { describeAdminError } from '../agent-access';
import { IAgentRun, IAgentStats } from '../agent.models';
import { AgentService } from '../agent.service';

/** Every saved answer, newest first, with totals at the top. Click a row for its full timeline. */
@Component({
  selector: 'app-agent-runs',
  templateUrl: './agent-runs.component.html',
  styleUrls: ['./agent-runs.component.scss']
})
export class AgentRunsComponent implements OnInit {
  runs: IAgentRun[] = [];
  stats: IAgentStats | null = null;
  totalCount = 0;
  page = 1;
  pageSize = 20;
  loading = false;
  errorMessage: string | null = null;

  constructor(private agentService: AgentService, private router: Router) { }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.errorMessage = null;

    this.agentService.getStats().subscribe(stats => this.stats = stats, () => this.stats = null);
    this.agentService.getRuns(this.page, this.pageSize).subscribe(page => {
      this.runs = page.items;
      this.totalCount = page.totalCount;
      this.loading = false;
    }, (error: HttpErrorResponse) => {
      this.loading = false;
      this.runs = [];
      this.errorMessage = describeAdminError(error);
    });
  }

  onPageChanged(page: number): void {
    if (page !== this.page) {
      this.page = page;
      this.load();
    }
  }

  open(run: IAgentRun): void {
    this.router.navigate(['/agent/runs', run.id]);
  }

  status(run: IAgentRun): { label: string; css: string } {
    if (run.error) {
      return { label: 'error', css: 'badge-danger' };
    }
    if (run.blocked) {
      return { label: 'blocked', css: 'badge-warning' };
    }
    if (run.stopReason) {
      return { label: 'stopped', css: 'badge-warning' };
    }
    return { label: 'answered', css: 'badge-success' };
  }
}
