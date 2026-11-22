import { AfterViewChecked, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subscription } from 'rxjs';
import { AccountService } from 'src/app/account/account.service';
import { IUser } from 'src/app/shared/models/user';
import { IAgentInfo, IAgentResult, IChatMessage } from '../agent.models';
import { AgentService } from '../agent.service';

const SESSION_KEY = 'agent-chat-session';
const MESSAGES_KEY = 'agent-chat-messages';

/**
 * The chat page: messages on the left, and on the right the "how was this answer made"
 * panel for the selected answer. The conversation is kept for this browser tab
 * (sessionStorage), so a page reload does not lose it.
 */
@Component({
  selector: 'app-agent-chat',
  templateUrl: './agent-chat.component.html',
  styleUrls: ['./agent-chat.component.scss']
})
export class AgentChatComponent implements OnInit, OnDestroy, AfterViewChecked {
  @ViewChild('messageList') messageList: ElementRef<HTMLElement>;

  messages: IChatMessage[] = [];
  selected: IAgentResult | null = null;
  draft = '';
  sending = false;
  sessionId: string | null = null;
  info: IAgentInfo | null = null;
  currentUser$: Observable<IUser>;
  maxChars = 2000;

  readonly suggestions = [
    'Show me red gloves under $50',
    'Can I return a board after 5 weeks?',
    'What delivery options do you have?',
    "What's your cheapest hat and how fast can it arrive?",
    'Show me my orders'
  ];

  private scrollPending = false;
  private sub: Subscription;

  constructor(private agentService: AgentService, private accountService: AccountService) { }

  ngOnInit(): void {
    this.currentUser$ = this.accountService.currentUser$;
    this.restore();
    this.sub = this.agentService.getInfo().subscribe(info => {
      this.info = info;
      this.maxChars = info.maxInputChars;
    }, () => { /* info is optional */ });
  }

  ngOnDestroy(): void {
    if (this.sub) {
      this.sub.unsubscribe();
    }
  }

  ngAfterViewChecked(): void {
    if (this.scrollPending && this.messageList) {
      const el = this.messageList.nativeElement;
      el.scrollTop = el.scrollHeight;
      this.scrollPending = false;
    }
  }

  send(text?: string): void {
    const message = (text !== undefined ? text : this.draft).trim();
    if (!message || this.sending) {
      return;
    }
    if (message.length > this.maxChars) {
      this.addNotice('That message is too long (' + message.length + ' characters, the limit is ' + this.maxChars + ').');
      return;
    }

    this.messages.push({ role: 'user', text: message });
    this.draft = '';
    this.sending = true;
    this.scrollPending = true;

    this.agentService.chat(message, this.sessionId).subscribe(result => {
      this.sessionId = result.sessionId;
      this.messages.push({ role: 'assistant', text: result.answer, result });
      this.selected = result;
      this.sending = false;
      this.scrollPending = true;
      this.save();
    }, (error: HttpErrorResponse) => {
      this.sending = false;
      this.addNotice(this.describeError(error));
    });
  }

  /** Enter sends, Shift+Enter makes a new line. */
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  select(message: IChatMessage): void {
    if (message.result) {
      this.selected = message.result;
    }
  }

  newChat(): void {
    this.messages = [];
    this.selected = null;
    this.sessionId = null;
    this.draft = '';
    this.save();
  }

  private addNotice(text: string): void {
    this.messages.push({ role: 'notice', text });
    this.scrollPending = true;
    this.save();
  }

  private describeError(error: HttpErrorResponse): string {
    if (error.status === 429) {
      return (error.error && error.error.message) || 'Too many messages. Please wait a minute and try again.';
    }
    if (error.status === 0) {
      return 'Could not reach the server. Is the API running?';
    }
    if (error.error && error.error.message) {
      return error.error.message;
    }
    return 'Something went wrong (HTTP ' + error.status + '). Please try again.';
  }

  private save(): void {
    try {
      if (this.sessionId) {
        sessionStorage.setItem(SESSION_KEY, this.sessionId);
      } else {
        sessionStorage.removeItem(SESSION_KEY);
      }
      sessionStorage.setItem(MESSAGES_KEY, JSON.stringify(this.messages));
    } catch {
      // Storage can be full or blocked; the chat still works without it.
    }
  }

  private restore(): void {
    try {
      this.sessionId = sessionStorage.getItem(SESSION_KEY);
      const saved = sessionStorage.getItem(MESSAGES_KEY);
      this.messages = saved ? JSON.parse(saved) : [];
      const answers = this.messages.filter(m => !!m.result);
      this.selected = answers.length > 0 ? answers[answers.length - 1].result : null;
      this.scrollPending = true;
    } catch {
      this.messages = [];
    }
  }
}
