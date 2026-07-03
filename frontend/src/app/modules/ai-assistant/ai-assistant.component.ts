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
  private chatSubscription?: Subscription;

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

    this.chatSubscription = this.apiService.postChatMessage(userMessage, history).subscribe({
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
        this.isLoading = false;
        this.chatSubscription = undefined;
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
    const headers = Object.keys(data[0]);
    const csvRows = [];
    
    csvRows.push(headers.map(header => `"${header.replace(/"/g, '""')}"`).join(','));
    
    for (const row of data) {
      const values = headers.map(header => {
        const val = row[header];
        const escaped = ('' + (val ?? '')).replace(/"/g, '""');
        return `"${escaped}"`;
      });
      csvRows.push(values.join(','));
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
    const headers = Object.keys(data[0]);
    const rows = [];
    
    rows.push(headers.join('\t'));
    
    for (const row of data) {
      const values = headers.map(header => {
        const val = row[header];
        return ('' + (val ?? '')).replace(/\r?\n/g, ' ');
      });
      rows.push(values.join('\t'));
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
    this.messages = [];
    this.ngOnInit();
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
}
