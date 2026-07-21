import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss']
})
export class ProfileComponent implements OnInit {
  private http = inject(HttpClient);

  // Edit Mode state
  isEditing = false;

  // Personal Information
  fullName = 'Thalaimalai';
  shortName = 'Thalaimalai';
  username = 'Thalaimalai';
  mobile = '+91 98765 43210';

  // Email Details & App Password (Added before Address)
  userEmail = 'sanjay.s@ariyaitech.com';
  appPassword = '';
  imapServer = 'imap.gmail.com';
  imapPort = 993;
  smtpServer = 'smtp.gmail.com';
  smtpPort = 465;
  useSsl = true;

  showAppPassword = false;
  showAdvancedEmailSettings = false;

  // Address
  addressLine1 = '';
  addressLine2 = '';
  city = '';
  stateCountry = '';

  // Account Details (Read-only System Metadata)
  userId = '2';
  roleId = '0';
  roleType = '-';
  categoryId = '0';
  userGroupId = '46';
  groupName = 'Naren-Marketing';
  companyId = '7';
  agentId = '97';
  empId = '64';
  allowEdit = '-';
  status = 'Active';

  // Loading & Feedback States
  isLoading = false;
  isTestingEmail = false;
  isSaving = false;
  feedbackMessage = '';
  feedbackType: 'success' | 'error' = 'success';

  ngOnInit() {
    this.loadProfileAndEmailSettings();
  }

  loadProfileAndEmailSettings() {
    this.isLoading = true;

    // Load User / Agent Profile info
    this.http.get<any>('http://localhost:5022/api/Auth/me').subscribe({
      next: (agent) => {
        if (agent) {
          this.fullName = agent.name || this.fullName;
          this.shortName = agent.name || this.shortName;
          this.username = agent.email ? agent.email.split('@')[0] : this.username;
          this.mobile = agent.phone || this.mobile;
          if (agent.email) {
            this.userEmail = agent.email;
          }
        }
      },
      error: () => {
        // Fallback silently if unauthenticated or error
      }
    });

    // Load Email Integration Settings
    this.http.get<any>('http://localhost:5022/api/email-settings').subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res) {
          this.imapServer = res.imapServer || 'imap.gmail.com';
          this.imapPort = res.imapPort || 993;
          this.smtpServer = res.smtpServer || 'smtp.gmail.com';
          this.smtpPort = res.smtpPort || 465;
          this.useSsl = res.useSsl !== undefined ? res.useSsl : true;

          if (res.imapUsername) {
            this.userEmail = res.imapUsername;
          }
          if (res.imapPassword) {
            this.appPassword = res.imapPassword;
          }
        }
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  getInitials(): string {
    if (!this.fullName) return 'TH';
    const parts = this.fullName.trim().split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return this.fullName.substring(0, 2).toUpperCase();
  }

  toggleEditMode() {
    this.isEditing = !this.isEditing;
  }

  toggleAppPasswordVisibility() {
    this.showAppPassword = !this.showAppPassword;
  }

  toggleAdvancedEmail() {
    this.showAdvancedEmailSettings = !this.showAdvancedEmailSettings;
  }

  testEmailConnection() {
    this.isTestingEmail = true;
    this.feedbackMessage = '';

    const payload = {
      imapServer: this.imapServer,
      imapPort: this.imapPort,
      imapUsername: this.userEmail,
      imapPassword: this.appPassword,
      smtpServer: this.smtpServer,
      smtpPort: this.smtpPort,
      smtpUsername: this.userEmail,
      smtpPassword: this.appPassword,
      useSsl: this.useSsl
    };

    this.http.post<any>('http://localhost:5022/api/email-settings/test', payload).subscribe({
      next: () => {
        this.isTestingEmail = false;
        this.showFeedback('Email integration test successful!', 'success');
      },
      error: (err) => {
        this.isTestingEmail = false;
        this.showFeedback(err.error?.detail || 'Email test failed. Please verify your App Password.', 'error');
      }
    });
  }

  saveProfile() {
    this.isSaving = true;
    this.feedbackMessage = '';

    // Save Email Settings payload
    const emailPayload = {
      imapServer: this.imapServer,
      imapPort: this.imapPort,
      imapUsername: this.userEmail,
      imapPassword: this.appPassword,
      smtpServer: this.smtpServer,
      smtpPort: this.smtpPort,
      smtpUsername: this.userEmail,
      smtpPassword: this.appPassword,
      useSsl: this.useSsl
    };

    this.http.post<any>('http://localhost:5022/api/email-settings', emailPayload).subscribe({
      next: () => {
        this.isSaving = false;
        this.isEditing = false;
        if (this.appPassword && this.appPassword !== '********') {
          this.appPassword = '********';
        }
        this.showFeedback('Profile & Email details updated successfully!', 'success');
      },
      error: (err) => {
        this.isSaving = false;
        this.showFeedback(err.error?.detail || 'Failed to update profile.', 'error');
      }
    });
  }

  showFeedback(msg: string, type: 'success' | 'error') {
    this.feedbackMessage = msg;
    this.feedbackType = type;
    setTimeout(() => {
      this.feedbackMessage = '';
    }, 5000);
  }
}
