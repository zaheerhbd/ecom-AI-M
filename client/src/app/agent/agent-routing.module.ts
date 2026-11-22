import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AgentChatComponent } from './agent-chat/agent-chat.component';
import { AgentEvalsComponent } from './agent-evals/agent-evals.component';
import { AgentHowItWorksComponent } from './agent-how-it-works/agent-how-it-works.component';
import { AgentRunDetailComponent } from './agent-run-detail/agent-run-detail.component';
import { AgentRunsComponent } from './agent-runs/agent-runs.component';
import { AgentShellComponent } from './agent-shell/agent-shell.component';

// /agent/chat, /agent/runs, /agent/runs/:id, /agent/evals, /agent/how-it-works
// The runs and evals pages need the Admin role in production (the API enforces it);
// in Development they are open to everyone.
const routes: Routes = [
  {
    path: '',
    component: AgentShellComponent,
    children: [
      { path: '', redirectTo: 'chat', pathMatch: 'full' },
      { path: 'chat', component: AgentChatComponent, data: { breadcrumb: 'Chat' } },
      { path: 'runs', component: AgentRunsComponent, data: { breadcrumb: 'Runs' } },
      { path: 'runs/:id', component: AgentRunDetailComponent, data: { breadcrumb: { alias: 'AgentRunDetail' } } },
      { path: 'evals', component: AgentEvalsComponent, data: { breadcrumb: 'Evals' } },
      { path: 'how-it-works', component: AgentHowItWorksComponent, data: { breadcrumb: 'How it works' } }
    ]
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AgentRoutingModule { }
