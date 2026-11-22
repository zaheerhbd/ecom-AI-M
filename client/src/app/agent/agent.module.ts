import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SharedModule } from '../shared/shared.module';
import { AgentRoutingModule } from './agent-routing.module';
import { AgentShellComponent } from './agent-shell/agent-shell.component';
import { AgentChatComponent } from './agent-chat/agent-chat.component';
import { AgentTimelineComponent } from './agent-timeline/agent-timeline.component';
import { AgentRunsComponent } from './agent-runs/agent-runs.component';
import { AgentRunDetailComponent } from './agent-run-detail/agent-run-detail.component';
import { AgentEvalsComponent } from './agent-evals/agent-evals.component';
import { AgentHowItWorksComponent } from './agent-how-it-works/agent-how-it-works.component';
import { AgentMarkdownPipe, PrettyJsonPipe } from './agent-pipes';

/** The Shop Assistant pages (Phase 4). Lazy loaded at /agent, like the shop and basket modules. */
@NgModule({
  declarations: [
    AgentShellComponent,
    AgentChatComponent,
    AgentTimelineComponent,
    AgentRunsComponent,
    AgentRunDetailComponent,
    AgentEvalsComponent,
    AgentHowItWorksComponent,
    AgentMarkdownPipe,
    PrettyJsonPipe
  ],
  imports: [
    CommonModule,
    SharedModule,
    AgentRoutingModule
  ]
})
export class AgentModule { }
