import { Component } from '@angular/core';

/** The frame around every Assistant page: the tab bar and the page below it. */
@Component({
  selector: 'app-agent-shell',
  template: `
    <div class="container-fluid px-4 mt-4 mb-4">
      <ul class="nav nav-tabs mb-3">
        <li class="nav-item">
          <a class="nav-link" routerLink="chat" routerLinkActive="active"><i class="fa fa-comments mr-1"></i>Chat</a>
        </li>
        <li class="nav-item">
          <a class="nav-link" routerLink="runs" routerLinkActive="active"><i class="fa fa-list mr-1"></i>Runs</a>
        </li>
        <li class="nav-item">
          <a class="nav-link" routerLink="evals" routerLinkActive="active"><i class="fa fa-check-square-o mr-1"></i>Evals</a>
        </li>
        <li class="nav-item">
          <a class="nav-link" routerLink="how-it-works" routerLinkActive="active"><i class="fa fa-question-circle mr-1"></i>How it works</a>
        </li>
      </ul>
      <router-outlet></router-outlet>
    </div>
  `
})
export class AgentShellComponent { }
