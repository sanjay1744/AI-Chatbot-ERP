import { Component, inject, OnInit, AfterViewInit, ViewChild, ElementRef, AfterViewChecked, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { CopilotStateService } from '../../services/copilot-state.service';
import { ChartRendererComponent } from './chart-renderer/chart-renderer.component';

interface Message {
  sender: 'user' | 'ai';
  text: string;
  sql?: string;
  data?: any[];
  chart?: any;
  timestamp: Date;
}

@Component({
  selector: 'app-ai-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule, ChartRendererComponent],
  templateUrl: './ai-assistant.component.html',
  styleUrls: ['./ai-assistant.component.scss']
})
export class AiAssistantComponent implements OnInit, AfterViewChecked, AfterViewInit {
  private apiService = inject(ApiService);
  private cdr = inject(ChangeDetectorRef);
  private copilotStateService = inject(CopilotStateService);

  @ViewChild('chatScrollContainer') private chatScrollContainer!: ElementRef;
  @ViewChild('chatInput') private chatInput!: ElementRef;

  userInput: string = '';
  messages: Message[] = [];
  isLoading: boolean = false;

  suggestionChips = [
    'What are the total sales across all records?',
    'Who is the top-performing sales agent?',
    'Show total purchases by customer Premier Cotton Textiles',
    'What is the most popular item sold by quantity?',
    'Show monthly sales trend for 2025'
  ];

  ngOnInit() {
    // Add initial welcome message
    this.messages.push({
      sender: 'ai',
      text: "Hi! I'm AriyAI, your intelligent ERP & CRM copilot. How can I help you today?",
      timestamp: new Date()
    });
  }

  ngAfterViewChecked() {
    this.scrollToBottom();
  }

  ngAfterViewInit() {
    this.focusInput();
  }

  focusInput(): void {
    setTimeout(() => {
      try {
        this.chatInput.nativeElement.focus();
      } catch (err) {
        // Ignore
      }
    }, 0);
  }

  sendMessage(text: string) {
    if (!text || text.trim() === '' || this.isLoading) {
      return;
    }

    const userMessage = text.trim();
    this.messages.push({
      sender: 'user',
      text: userMessage,
      timestamp: new Date()
    });

    this.userInput = '';
    this.isLoading = true;
    this.cdr.detectChanges();
    this.scrollToBottom();

    const history = this.messages
      .slice(0, -1)
      .map(m => ({ sender: m.sender, text: m.text }));

    this.apiService.postChatMessage(userMessage, history).subscribe({
      next: (res) => {
        this.messages.push({
          sender: 'ai',
          text: res.reply || 'Here is the data from the database.',
          sql: res.sql || '',
          data: res.data && res.data.length > 0 ? res.data : undefined,
          chart: res.chart || undefined,
          timestamp: new Date()
        });
        this.isLoading = false;
        this.cdr.detectChanges();
        this.scrollToBottom();
        this.focusInput();
      },
      error: (err) => {
        console.error(err);
        this.messages.push({
          sender: 'ai',
          text: 'Sorry, I encountered an error while communicating with the database backend. Please ensure the backend is running.',
          timestamp: new Date()
        });
        this.isLoading = false;
        this.cdr.detectChanges();
        this.scrollToBottom();
        this.focusInput();
      }
    });
  }

  selectChip(chip: string) {
    this.sendMessage(chip);
  }

  getTableHeaders(data: any[]): string[] {
    if (!data || data.length === 0) return [];
    return Object.keys(data[0]);
  }

  private scrollToBottom(): void {
    try {
      this.chatScrollContainer.nativeElement.scrollTop = this.chatScrollContainer.nativeElement.scrollHeight;
    } catch (err) {
      // Ignore
    }
  }

  clearChat() {
    this.messages = [];
    this.ngOnInit();
    this.focusInput();
  }

  closeCopilot() {
    this.copilotStateService.close();
  }
}
