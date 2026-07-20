import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-email-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './email-settings.component.html',
  styleUrls: ['./email-settings.component.scss']
})
export class EmailSettingsComponent implements OnInit {
  private http = inject(HttpClient);

  imapServer = 'imap.gmail.com';
  imapPort = 993;
  imapUsername = '';
  imapPassword = '';
  smtpServer = 'smtp.gmail.com';
  smtpPort = 465;
  smtpUsername = '';
  smtpPassword = '';
  useSsl = true;

  isLoading = false;
  isTesting = false;
  message = '';
  messageType: 'success' | 'error' = 'success';
  configured = false;

  ngOnInit() {
    this.loadSettings();
  }

  loadSettings() {
    this.isLoading = true;
    this.http.get<any>('http://localhost:5022/api/email-settings').subscribe({
      next: (res) => {
        this.isLoading = false;
        this.imapServer = res.imapServer || 'imap.gmail.com';
        this.imapPort = res.imapPort || 993;
        this.imapUsername = res.imapUsername || '';
        this.imapPassword = res.imapPassword || '';
        this.smtpServer = res.smtpServer || 'smtp.gmail.com';
        this.smtpPort = res.smtpPort || 465;
        this.smtpUsername = res.smtpUsername || '';
        this.smtpPassword = res.smtpPassword || '';
        this.useSsl = res.useSsl !== undefined ? res.useSsl : true;
        this.configured = res.configured;
      },
      error: (err) => {
        this.isLoading = false;
        this.showFeedback('Failed to load settings from server.', 'error');
      }
    });
  }

  sanitizeServers() {
    // Auto-correct server hosts if user entered email addresses by mistake
    if (this.imapServer && this.imapServer.includes('@')) {
      if (this.imapServer.toLowerCase().endsWith('@gmail.com')) {
        this.imapServer = 'imap.gmail.com';
      }
    }
    if (this.smtpServer && this.smtpServer.includes('@')) {
      if (this.smtpServer.toLowerCase().endsWith('@gmail.com')) {
        this.smtpServer = 'smtp.gmail.com';
      }
    }
  }

  onSave() {
    this.sanitizeServers();
    this.isLoading = true;
    const payload = {
      imapServer: this.imapServer,
      imapPort: this.imapPort,
      imapUsername: this.imapUsername,
      imapPassword: this.imapPassword,
      smtpServer: this.smtpServer,
      smtpPort: this.smtpPort,
      smtpUsername: this.smtpUsername,
      smtpPassword: this.smtpPassword,
      useSsl: this.useSsl
    };

    this.http.post<any>('http://localhost:5022/api/email-settings', payload).subscribe({
      next: (res) => {
        this.isLoading = false;
        this.configured = true;
        if (this.imapPassword && this.imapPassword !== '********') {
          this.imapPassword = '********';
        }
        if (this.smtpPassword && this.smtpPassword !== '********') {
          this.smtpPassword = '********';
        }
        this.showFeedback('Settings saved successfully.', 'success');
      },
      error: (err) => {
        this.isLoading = false;
        this.showFeedback(err.error?.detail || 'Failed to save settings.', 'error');
      }
    });
  }

  onTest() {
    this.sanitizeServers();
    this.isTesting = true;
    this.message = '';

    const payload = {
      imapServer: this.imapServer,
      imapPort: this.imapPort,
      imapUsername: this.imapUsername,
      imapPassword: this.imapPassword,
      smtpServer: this.smtpServer,
      smtpPort: this.smtpPort,
      smtpUsername: this.smtpUsername,
      smtpPassword: this.smtpPassword,
      useSsl: this.useSsl
    };

    this.http.post<any>('http://localhost:5022/api/email-settings/test', payload).subscribe({
      next: (res) => {
        this.isTesting = false;
        this.showFeedback('IMAP & SMTP connections verified successfully!', 'success');
      },
      error: (err) => {
        this.isTesting = false;
        this.showFeedback(err.error?.detail || 'Connection test failed.', 'error');
      }
    });
  }

  showFeedback(text: string, type: 'success' | 'error') {
    this.message = text;
    this.messageType = type;
  }

  hasInvalidServers(): boolean {
    const isInvalid = (val: string) => {
      if (!val) return false; // empty is handled by 'required'
      return val.includes('@') || !val.includes('.');
    };
    return isInvalid(this.imapServer) || isInvalid(this.smtpServer);
  }
}
