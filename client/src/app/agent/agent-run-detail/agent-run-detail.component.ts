import { Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { BreadcrumbService } from 'xng-breadcrumb';
import { describeAdminError } from '../agent-access';
import { IAgentRun } from '../agent.models';
import { AgentService } from '../agent.service';

/** One saved run: the question, the answer and the full timeline. */
@Component({
  selector: 'app-agent-run-detail',
  templateUrl: './agent-run-detail.component.html'
})
export class AgentRunDetailComponent implements OnInit {
  run: IAgentRun | null = null;
  errorMessage: string | null = null;

  constructor(private agentService: AgentService, private route: ActivatedRoute, private breadcrumbService: BreadcrumbService) {
    this.breadcrumbService.set('@AgentRunDetail', ' ');
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.agentService.getRun(id).subscribe(run => {
      this.run = run;
      this.breadcrumbService.set('@AgentRunDetail', 'Run ' + run.id.substring(0, 8));
    }, (error: HttpErrorResponse) => {
      this.errorMessage = describeAdminError(error);
    });
  }
}
