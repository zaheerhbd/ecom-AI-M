import { Component } from '@angular/core';
import { AiService } from '../services/ai.service';
import { IAiChatResponse } from 'src/app/shared/models/aiChat';

@Component({
  selector: 'app-ai-playground',
  templateUrl: './ai-playground.component.html',
  styleUrls: ['./ai-playground.component.scss']
})
export class AiPlaygroundComponent {
  draft = '';
  askedQuestion: string | null = null;
  loading = false;
  result: IAiChatResponse | null = null;
  error: string | null = null;

  suggestions = [
    'show me boots',
    'find affordable products under 100',
    'what delivery options do you have?'
  ];

  constructor(private aiService: AiService) { }

  ask(text?: string) {
    const question = (text ?? this.draft).trim();
    if (!question) {
      this.error = 'Please enter a question.';
      return;
    }

    this.draft = '';
    this.askedQuestion = question;
    this.result = null;
    this.loading = true;
    this.error = null;

    this.aiService.askQuestion(question).subscribe({
      next: (response) => {
        this.result = response;
        this.loading = false;
      },
      error: () => {
        this.error = 'The Knowledge Chat endpoint could not be reached. Make sure the API is running.';
        this.loading = false;
      }
    });
  }

  onKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.ask();
    }
  }
}
