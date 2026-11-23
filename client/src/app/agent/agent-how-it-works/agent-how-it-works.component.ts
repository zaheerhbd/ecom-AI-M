import { Component, OnInit } from '@angular/core';
import { IAgentInfo } from '../agent.models';
import { AgentService } from '../agent.service';

/** A plain-English explanation of how the assistant answers, with the live tool list from api/agent/info. */
@Component({
  selector: 'app-agent-how-it-works',
  templateUrl: './agent-how-it-works.component.html',
  styleUrls: ['./agent-how-it-works.component.scss']
})
export class AgentHowItWorksComponent implements OnInit {
  info: IAgentInfo | null = null;

  constructor(private agentService: AgentService) { }

  ngOnInit(): void {
    this.agentService.getInfo().subscribe(info => this.info = info, () => this.info = null);
  }
}
