import { Component, inject, OnInit, AfterViewInit, ViewChild, ElementRef, AfterViewChecked, ChangeDetectorRef, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { CopilotStateService } from '../../services/copilot-state.service';
import { ChartRendererComponent } from './chart-renderer/chart-renderer.component';

interface Message {
  sender: 'user' | 'ai';
  text: string;
  sql?: string;
  data?: any[];
  parsedData?: any[];
  chart?: any;
  timestamp: Date;
  filterText?: string;
  sortColumn?: string;
  sortAscending?: boolean;
}

@Component({
  selector: 'app-ai-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule, ChartRendererComponent],
  templateUrl: './ai-assistant.component.html',
  styleUrls: ['./ai-assistant.component.scss']
})
export class AiAssistantComponent implements OnInit, AfterViewChecked, AfterViewInit, OnDestroy {
  private apiService = inject(ApiService);
  private cdr = inject(ChangeDetectorRef);
  private copilotStateService = inject(CopilotStateService);

  @ViewChild('chatScrollContainer') private chatScrollContainer!: ElementRef;
  @ViewChild('chatInput') private chatInput!: ElementRef;

  userInput: string = '';
  messages: Message[] = [];
  isLoading: boolean = false;
  activeModalMessage: Message | null = null;
  activeModalChartMessage: Message | null = null;
  private chatSubscription?: Subscription;

  // Chat History Sidebar states
  isHistoryOpen: boolean = false;
  sessions: any[] = [];
  filteredSessions: any[] = [];
  currentSessionId: number | null = null;
  historySearchQuery: string = '';
  editingSessionId: number | null = null;
  editingTitle: string = '';

  // Voice Input (Speech-to-Text) states
  isSpeechSupported: boolean = false;
  isListening: boolean = false;
  private recognition: any;

  suggestionChips = [
    'What are the total sales across all records?',
    'Who is the top-performing sales agent?',
    'Show total purchases by customer Premier Cotton Textiles',
    'What is the most popular item sold by quantity?',
    'Show monthly sales trend for 2025'
  ];

  ngOnInit() {
    this.currentSessionId = null;
    this.messages = [];
    this.messages.push({
      sender: 'ai',
      text: "Hi! I'm AriyAI, your intelligent ERP & CRM copilot. How can I help you today?",
      timestamp: new Date()
    });
    this.loadSessions();
    this.initSpeechRecognition();
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

  // History operations
  toggleHistory(open?: boolean) {
    this.isHistoryOpen = open !== undefined ? open : !this.isHistoryOpen;
    this.copilotStateService.isExpanded.set(this.isHistoryOpen);
    if (this.isHistoryOpen) {
      this.loadSessions();
    }
  }

  loadSessions() {
    this.apiService.getChatSessions().subscribe({
      next: (res) => {
        this.sessions = res;
        this.filterSessions();
        this.cdr.detectChanges();
      },
      error: (err) => console.error('Failed to load chat sessions', err)
    });
  }

  filterSessions() {
    if (!this.historySearchQuery || this.historySearchQuery.trim() === '') {
      this.filteredSessions = [...this.sessions];
    } else {
      const q = this.historySearchQuery.toLowerCase();
      this.filteredSessions = this.sessions.filter(s => 
        s.title && s.title.toLowerCase().includes(q)
      );
    }
  }

  selectSession(sessionId: number) {
    if (this.isLoading) return;
    this.currentSessionId = sessionId;
    this.isLoading = true;
    this.cdr.detectChanges();

    this.apiService.getChatSession(sessionId).subscribe({
      next: (res) => {
        const loadedMessages: Message[] = [];
        if (res.messages && res.messages.length > 0) {
          res.messages.forEach((m: any) => {
            const data = m.data && m.data.length > 0 ? m.data : undefined;
            let parsedData: any[] | undefined = undefined;
            if (!data && m.sender === 'ai') {
              parsedData = this.parseMessageForLists(m.text);
            }

            loadedMessages.push({
              sender: m.sender as 'user' | 'ai',
              text: m.text,
              sql: m.sql || '',
              data: data,
              parsedData: parsedData,
              chart: m.chart || undefined,
              timestamp: new Date(m.timestamp),
              filterText: '',
              sortColumn: '',
              sortAscending: true
            });
          });
          this.messages = loadedMessages;
        } else {
          this.messages = [];
        }
        this.isLoading = false;
        this.toggleHistory(false); // Close history drawer
        this.cdr.detectChanges();
        this.scrollToBottom();
        this.focusInput();
      },
      error: (err) => {
        console.error('Failed to load session details', err);
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  startRename(session: any) {
    this.editingSessionId = session.id;
    this.editingTitle = session.title;
    setTimeout(() => {
      const inputEl = document.querySelector('.item-title-block input') as HTMLInputElement;
      if (inputEl) {
        inputEl.focus();
        inputEl.select();
      }
    }, 50);
  }

  saveRename(sessionId: number) {
    if (!this.editingTitle || this.editingTitle.trim() === '') {
      this.editingSessionId = null;
      return;
    }

    const title = this.editingTitle.trim();
    this.apiService.renameChatSession(sessionId, title).subscribe({
      next: () => {
        const session = this.sessions.find(s => s.id === sessionId);
        if (session) {
          session.title = title;
        }
        this.filterSessions();
        this.editingSessionId = null;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed to rename session', err);
        this.editingSessionId = null;
        this.cdr.detectChanges();
      }
    });
  }

  deleteSession(sessionId: number) {
    if (confirm('Are you sure you want to delete this conversation?')) {
      this.apiService.deleteChatSession(sessionId).subscribe({
        next: () => {
          this.sessions = this.sessions.filter(s => s.id !== sessionId);
          this.filterSessions();
          if (this.currentSessionId === sessionId) {
            this.clearChat();
          }
          this.cdr.detectChanges();
        },
        error: (err) => console.error('Failed to delete session', err)
      });
    }
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

    this.chatSubscription = this.apiService.postChatMessage(userMessage, this.currentSessionId).subscribe({
      next: (res) => {
        const text = res.reply || 'Here is the data from the database.';
        const data = res.data && res.data.length > 0 ? res.data : undefined;
        let parsedData: any[] | undefined = undefined;
        if (!data) {
          parsedData = this.parseMessageForLists(text);
        }

        this.messages.push({
          sender: 'ai',
          text: text,
          sql: res.sql || '',
          data: data,
          parsedData: parsedData,
          chart: res.chart || undefined,
          timestamp: new Date(),
          filterText: '',
          sortColumn: '',
          sortAscending: true
        });

        const isNew = this.currentSessionId === null;
        if (res.sessionId) {
          this.currentSessionId = res.sessionId;
        }

        this.isLoading = false;
        this.chatSubscription = undefined;
        
        if (isNew) {
          this.loadSessions();
        }

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
        this.chatSubscription = undefined;
        this.cdr.detectChanges();
        this.scrollToBottom();
        this.focusInput();
      }
    });
  }

  stopChat() {
    if (this.chatSubscription) {
      this.chatSubscription.unsubscribe();
      this.chatSubscription = undefined;
    }
    this.isLoading = false;
    this.messages.push({
      sender: 'ai',
      text: 'Process stopped by user.',
      timestamp: new Date()
    });
    this.cdr.detectChanges();
    this.scrollToBottom();
    this.focusInput();
  }

  ngOnDestroy() {
    if (this.chatSubscription) {
      this.chatSubscription.unsubscribe();
      this.chatSubscription = undefined;
    }
    if (this.recognition && this.isListening) {
      this.recognition.stop();
    }
  }

  initSpeechRecognition() {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
    this.isSpeechSupported = !!SpeechRecognition;
    
    if (this.isSpeechSupported) {
      this.recognition = new SpeechRecognition();
      this.recognition.continuous = false;
      this.recognition.interimResults = false;
      this.recognition.lang = 'en-IN';

      this.recognition.onstart = () => {
        this.isListening = true;
        this.cdr.detectChanges();
      };

      this.recognition.onend = () => {
        this.isListening = false;
        this.cdr.detectChanges();
      };

      this.recognition.onresult = (event: any) => {
        const transcript = event.results[0][0].transcript;
        if (transcript) {
          this.userInput = transcript;
          this.cdr.detectChanges();
        }
      };

      this.recognition.onerror = (event: any) => {
        console.error('Speech recognition error:', event.error);
        this.isListening = false;
        this.cdr.detectChanges();
      };
    }
  }

  toggleVoiceInput() {
    if (!this.isSpeechSupported || this.isLoading) return;

    if (this.isListening) {
      this.recognition.stop();
    } else {
      try {
        this.userInput = '';
        this.recognition.start();
      } catch (err) {
        console.error('Failed to start speech recognition:', err);
      }
    }
  }

  selectChip(chip: string) {
    this.sendMessage(chip);
  }

  getTableHeaders(data: any[]): string[] {
    if (!data || data.length === 0) return [];
    return Object.keys(data[0]);
  }

  parseMessageForLists(text: string): any[] | undefined {
    if (!text) return undefined;

    // 1. Try to find a CSV block delimited by ```csv ... ```
    const csvBlockRegex = /```csv\r?\n([\s\S]*?)```/i;
    const blockMatch = text.match(csvBlockRegex);
    if (blockMatch) {
      const parsedCsv = this.parseCSVText(blockMatch[1]);
      if (parsedCsv && parsedCsv.length > 0) {
        return parsedCsv;
      }
    }

    // 2. Try to detect if the entire text looks like CSV (at least 2 lines with consistent commas)
    const parsedCsvDirect = this.parseCSVText(text);
    if (parsedCsvDirect && parsedCsvDirect.length > 0) {
      return parsedCsvDirect;
    }

    // 3. Try to detect bulleted or numbered list items
    const parsedList = this.parseMarkdownList(text);
    if (parsedList && parsedList.length > 0) {
      return parsedList;
    }

    return undefined;
  }

  parseCSVText(csvText: string): any[] | null {
    const lines = csvText.trim().split('\n');
    if (lines.length < 2) return null;

    // Count commas in first line to determine header count
    const headerCommas = (lines[0].match(/,/g) || []).length;
    if (headerCommas === 0) return null;

    // Check if subsequent non-empty lines have similar comma counts
    let consistent = true;
    let dataLineCount = 0;
    for (let i = 1; i < lines.length; i++) {
      const line = lines[i].trim();
      if (!line) continue;
      dataLineCount++;
      const commas = (line.match(/,/g) || []).length;
      if (commas === 0 || Math.abs(commas - headerCommas) > 1) {
        consistent = false;
        break;
      }
    }

    if (!consistent || dataLineCount === 0) return null;

    try {
      const headers = lines[0].split(',').map(h => h.trim().replace(/^["']|["']$/g, ''));
      const list: any[] = [];
      for (let i = 1; i < lines.length; i++) {
        const line = lines[i].trim();
        if (!line) continue;
        const values = this.splitCSVLine(line);
        const row: any = {};
        headers.forEach((header, index) => {
          row[header] = (values[index] ?? '').trim().replace(/^["']|["']$/g, '');
        });
        list.push(row);
      }
      return list;
    } catch (e) {
      return null;
    }
  }

  splitCSVLine(line: string): string[] {
    const result = [];
    let current = '';
    let inQuotes = false;
    for (let i = 0; i < line.length; i++) {
      const char = line[i];
      if (char === '"' || char === "'") {
        inQuotes = !inQuotes;
      } else if (char === ',' && !inQuotes) {
        result.push(current);
        current = '';
      } else {
        current += char;
      }
    }
    result.push(current);
    return result;
  }

  parseMarkdownList(text: string): any[] | null {
    const lines = text.split('\n');
    const items: string[] = [];
    const listPattern = /^([-*•]|\d+[.)])\s+(.+)$/;

    for (let line of lines) {
      line = line.trim();
      if (!line) continue;
      const match = line.match(listPattern);
      if (match) {
        const content = match[2].trim();
        if (content.length > 0 && content.length < 150) {
          items.push(content);
        }
      }
    }

    if (items.length > 1) {
      return items.map(item => ({ 'Item': item }));
    }
    return null;
  }

  getFilteredData(msg: Message): any[] {
    let data = msg.data || msg.parsedData || [];
    if (data.length === 0) return [];

    if (msg.filterText && msg.filterText.trim()) {
      const search = msg.filterText.toLowerCase().trim();
      data = data.filter(row => {
        return Object.values(row).some(val => 
          ('' + (val ?? '')).toLowerCase().includes(search)
        );
      });
    }

    if (msg.sortColumn) {
      const col = msg.sortColumn;
      const isAsc = msg.sortAscending ?? true;
      data = [...data].sort((a, b) => {
        const valA = a[col];
        const valB = b[col];
        
        const numA = Number(valA);
        const numB = Number(valB);
        if (!isNaN(numA) && !isNaN(numB)) {
          return isAsc ? numA - numB : numB - numA;
        }

        const strA = ('' + (valA ?? '')).toLowerCase();
        const strB = ('' + (valB ?? '')).toLowerCase();
        return isAsc ? strA.localeCompare(strB) : strB.localeCompare(strA);
      });
    }

    return data;
  }

  toggleSort(msg: Message, col: string) {
    if (msg.sortColumn === col) {
      msg.sortAscending = !msg.sortAscending;
    } else {
      msg.sortColumn = col;
      msg.sortAscending = true;
    }
  }

  exportToCsv(data: any[], filename: string) {
    if (!data || data.length === 0) return;
    const headers = ['S.No', ...Object.keys(data[0])];
    const csvRows = [];
    
    csvRows.push(headers.map(header => `"${header.replace(/"/g, '""')}"`).join(','));
    
    let index = 1;
    for (const row of data) {
      const values = headers.map(header => {
        if (header === 'S.No') return `"${index}"`;
        const val = row[header];
        const escaped = ('' + (val ?? '')).replace(/"/g, '""');
        return `"${escaped}"`;
      });
      csvRows.push(values.join(','));
      index++;
    }
    
    const csvContent = '\uFEFF' + csvRows.join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  exportToExcel(data: any[], filename: string) {
    this.exportToCsv(data, filename.endsWith('.csv') ? filename.replace('.csv', '.csv') : filename + '.csv');
  }

  copyTableToClipboard(data: any[]) {
    if (!data || data.length === 0) return;
    const headers = ['S.No', ...Object.keys(data[0])];
    const rows = [];
    
    rows.push(headers.join('\t'));
    
    let index = 1;
    for (const row of data) {
      const values = headers.map(header => {
        if (header === 'S.No') return index.toString();
        const val = row[header];
        return ('' + (val ?? '')).replace(/\r?\n/g, ' ');
      });
      rows.push(values.join('\t'));
      index++;
    }
    
    const tsvContent = rows.join('\n');
    navigator.clipboard.writeText(tsvContent).then(() => {
      // Intrusive alert avoided
    }).catch(err => {
      console.error('Failed to copy text: ', err);
    });
  }

  exportMessageText(msg: Message) {
    const dateStr = msg.timestamp.toISOString().split('T')[0];
    const textContent = `AriyAI Chat Message - ${dateStr}\nSender: ${msg.sender.toUpperCase()}\n\n${msg.text}`;
    const blob = new Blob([textContent], { type: 'text/plain;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', `message_${msg.sender}_${dateStr}.txt`);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  exportWholeChat() {
    if (this.messages.length === 0) return;
    
    let chatLog = 'AriyAI Chat History Export\n';
    chatLog += `Exported on: ${new Date().toLocaleString()}\n`;
    chatLog += '='.repeat(50) + '\n\n';
    
    for (const msg of this.messages) {
      const timeStr = msg.timestamp.toLocaleTimeString();
      const senderName = msg.sender === 'user' ? 'USER' : 'ARIYAI';
      chatLog += `[${timeStr}] ${senderName}:\n${msg.text}\n`;
      if (msg.sql) {
        chatLog += `Executed SQL: ${msg.sql}\n`;
      }
      chatLog += '-'.repeat(30) + '\n\n';
    }

    const blob = new Blob([chatLog], { type: 'text/plain;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', `chat_history_${new Date().toISOString().split('T')[0]}.txt`);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  private scrollToBottom(): void {
    try {
      this.chatScrollContainer.nativeElement.scrollTop = this.chatScrollContainer.nativeElement.scrollHeight;
    } catch (err) {
      // Ignore
    }
  }

  clearChat() {
    this.currentSessionId = null;
    this.messages = [];
    this.messages.push({
      sender: 'ai',
      text: "Hi! I'm AriyAI, your intelligent ERP & CRM copilot. How can I help you today?",
      timestamp: new Date()
    });
    this.focusInput();
  }

  closeCopilot() {
    this.copilotStateService.close();
  }

  openTableModal(msg: Message, sortCol?: string) {
    this.activeModalMessage = msg;
    if (sortCol) {
      this.toggleSort(msg, sortCol);
    }
  }

  closeTableModal() {
    this.activeModalMessage = null;
  }

  openChartModal(msg: Message) {
    this.activeModalChartMessage = msg;
  }

  closeChartModal() {
    this.activeModalChartMessage = null;
  }
}
