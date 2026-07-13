import { Component, HostListener, inject, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { timeout, catchError } from 'rxjs/operators';
import { throwError } from 'rxjs';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.scss']
})
export class HeaderComponent implements OnInit, OnDestroy {
  private router = inject(Router);
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);
  private sanitizer = inject(DomSanitizer);

  userName = 'Thalaimalai';
  userRole = 'Naren Marketing';
  isUserMenuOpen = false;
  isNotificationsOpen = false;
  activeNotifTab = 'mails';
  isSyncing = false;
  isExtracting = false;
  
  // Email Details Modal State
  isEmailDetailsOpen = false;
  selectedMail: any = null;

  // Excel Preview Modal State
  isPreviewOpen = false;
  isPreviewLoading = false;
  previewFilename = '';
  previewSheets: any[] = [];
  activePreviewSheetIdx = 0;

  // PDF Preview Modal State
  isPdfPreviewOpen = false;
  pdfUrl: SafeResourceUrl | null = null;
  pdfFilename = '';
  
  // Toast notification state
  toastMessage = '';
  toastType: 'success' | 'error' | 'info' = 'success';
  isToastVisible = false;
  private toastTimeout: any;

  private pollInterval: any;

  mockMails = [
    {
      id: 1,
      sender: 'Sanjay S',
      subject: 'enquiry',
      snippet: 'test email ...',
      time: 'Jul 9 03:51 PM',
      isRead: false
    },
    {
      id: 2,
      sender: 'Uma Maheshwari',
      subject: 'Fwd: RFQ for Spares List (PDF Attachment)',
      snippet: '---------- Forwarded message ---------- Dear Team, Plea...',
      time: 'Jul 8 05:58 PM',
      isRead: false
    },
    {
      id: 3,
      sender: 'Augustine Cruzmuthu',
      subject: 'Enquiry for items',
      snippet: 'give me quote for this ...',
      time: 'Jul 8 04:43 PM',
      isRead: false
    },
    {
      id: 4,
      sender: 'Augustine Cruzmuthu',
      subject: 'Give me Enquiry data',
      snippet: 'dsvrkjbvjhznbx ...',
      time: 'Jul 8 04:39 PM',
      isRead: false
    }
  ];

  get unreadMailsCount(): number {
    return this.mockMails.filter(m => m.isRead === false).length;
  }

  mailDetailsMap: { [key: number]: any } = {
    1: {
      from: 'Sanjay S <ssanjay1742004@gmail.com>',
      to: 'sanjay.personal987@gmail.com',
      date: '7/7/2026, 3:17:26 PM',
      subject: 'RFQ: Materials list for control panel project',
      body: `Dear Sir/Madam,

Please send your best pricing for the below-listed items:
Brand Part Code Description Qty Unit
ABB A16-30-10 ABB Contactor A16-30-10 16A 3P 4 PCS
Siemens 5SL6332-7 Siemens MCB 32A 3P C Curve 2 PCS
Schneider NSYCRN33200 Schneider CRN Enclosure 300×300×200mm 1 PCS
Generic DIN-35-1M DIN Rail 35mm Standard 1 Meter 15 PCS
savio Pulley Savio Timing Belt Pulley 15T 10 PCS

Best regards,
Abraham George
Procurement Manager, Apex Automation Corp`
    },
    2: {
      from: 'Uma Maheshwari <uma.m@ariyaitech.com>',
      to: 'sanjay.personal987@gmail.com',
      date: '7/8/2026, 5:58:24 PM',
      subject: 'Fwd: RFQ for Spares List (PDF Attachment)',
      body: `---------- Forwarded message ----------
From: Uma Maheshwari <uma.m@ariyaitech.com>
Date: Wed, Jul 8, 2026 at 5:58 PM
Subject: RFQ for Spares List (PDF Attachment)
To: Naren Procurement <naren.procure@manjunatha.com>

Dear Team,

Please review the attached PDF spares listing and provide your competitive quotation.

Thanks,
Uma Maheshwari
CEO, Ariyaitech Solutions`
    },
    3: {
      from: 'Augustine Cruzmuthu <augustine@gmail.com>',
      to: 'sanjay.personal987@gmail.com',
      date: '7/8/2026, 4:43:11 PM',
      subject: 'Enquiry for items',
      body: `Hi,

Please check and send a quote for the following items:
1. Proximity Sensor 3-wire NPN - 5 NOS
2. Solid State Relay 25A - 2 NOS

Regards,
Augustine`
    },
    4: {
      from: 'Augustine Cruzmuthu <augustine@gmail.com>',
      to: 'sanjay.personal987@gmail.com',
      date: '7/8/2026, 4:39:05 PM',
      subject: 'Give me Enquiry data',
      body: `Hello,

Can you share the previous enquiry data for Premier Cotton Textiles?

Thanks,
Augustine`
    }
  };

  ngOnInit() {
    this.fetchEmails();
    // Poll for new emails every 2 minutes (120,000 ms)
    this.pollInterval = setInterval(() => {
      this.fetchEmails();
    }, 120000);
  }

  ngOnDestroy() {
    if (this.pollInterval) {
      clearInterval(this.pollInterval);
    }
  }

  fetchEmails() {
    this.http.get<any[]>('http://localhost:5022/api/emails').subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.mockMails = data.map(email => ({
            id: email.id,
            sender: this.cleanSenderName(email.sender),
            subject: email.subject,
            snippet: email.body ? (email.body.substring(0, 60) + (email.body.length > 60 ? '...' : '')) : '',
            time: this.formatEmailDate(email.receivedAt || email.received_at),
            rawBody: email.body,
            rawFrom: email.sender,
            rawTo: email.recipient,
            rawDate: email.receivedAt || email.received_at,
            attachments: (email.attachmentsJson || email.attachments_json) ? JSON.parse(email.attachmentsJson || email.attachments_json) : [],
            isRead: email.isRead
          }));
        }
      },
      error: (err) => {
        console.warn('Failed to fetch real emails from C# backend. Keeping mock list:', err);
      }
    });
  }

  cleanSenderName(sender: string): string {
    const match = sender.match(/(.*?)\s*<(.*?)>/);
    if (match) {
      return match[1].trim().replace(/^["']|["']$/g, '') || match[2].trim();
    }
    return sender;
  }

  formatEmailDate(dateStr: string): string {
    if (!dateStr) return '';
    try {
      const date = new Date(dateStr);
      if (isNaN(date.getTime())) {
        return dateStr || '';
      }
      const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
      const month = months[date.getMonth()];
      const day = date.getDate();
      let hours = date.getHours();
      const minutes = date.getMinutes().toString().padStart(2, '0');
      const ampm = hours >= 12 ? 'PM' : 'AM';
      hours = hours % 12;
      hours = hours ? hours : 12;
      return `${month} ${day} ${hours.toString().padStart(2, '0')}:${minutes} ${ampm}`;
    } catch (e) {
      return dateStr || '';
    }
  }

  toggleUserMenu(event?: Event) {
    if (event) {
      event.stopPropagation();
    }
    this.isUserMenuOpen = !this.isUserMenuOpen;
    this.isNotificationsOpen = false;
  }

  toggleNotifications(event?: Event) {
    if (event) {
      event.stopPropagation();
    }
    this.isNotificationsOpen = !this.isNotificationsOpen;
    this.isUserMenuOpen = false;
  }

  selectNotifTab(tab: string) {
    this.activeTabChange(tab);
  }

  activeTabChange(tab: string) {
    this.activeNotifTab = tab;
  }

  syncGmail() {
    console.log('syncGmail() started, isSyncing = true');
    this.isSyncing = true;
    this.cdr.detectChanges();

    // Safety fallback: force-reset after 20s no matter what
    const safetyTimer = setTimeout(() => {
      if (this.isSyncing) {
        console.warn('syncGmail safety timer fired! Resetting isSyncing.');
        this.isSyncing = false;
        this.showToast('Sync timed out. Try again.', 'error');
        this.cdr.detectChanges();
      }
    }, 20000);

    this.http.post('http://localhost:5022/api/emails/sync', {}).pipe(
      timeout(18000), // 18s HTTP timeout (before the 20s safety)
      catchError(err => {
        console.error('syncGmail error caught in pipe:', err);
        clearTimeout(safetyTimer);
        this.isSyncing = false;
        if (err.name === 'TimeoutError') {
          this.showToast('Sync timed out. Try again.', 'error');
        } else {
          this.showToast('Sync failed. Check backend configuration.', 'error');
        }
        this.cdr.detectChanges();
        return throwError(() => err);
      })
    ).subscribe({
      next: (res: any) => {
        console.log('syncGmail response received:', res);
        clearTimeout(safetyTimer);
        this.fetchEmails();
        this.isSyncing = false;
        console.log('syncGmail finished next handler. isSyncing = false');
        if (res.new_emails_synced > 0) {
          this.showToast(`Synced ${res.new_emails_synced} new email(s).`, 'success');
        } else {
          this.showToast('No new emails received.', 'info');
        }
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('syncGmail subscription error handler:', err);
        this.isSyncing = false;
        this.cdr.detectChanges();
      }
    });
  }

  openMailDetails(mail: any) {
    this.selectedMail = {
      id: mail.id,
      from: mail.rawFrom || mail.sender,
      to: mail.rawTo || 'sanjay.personal987@gmail.com',
      date: mail.rawDate ? this.formatEmailDate(mail.rawDate) : mail.time,
      subject: mail.subject,
      body: mail.rawBody || (this.mailDetailsMap[mail.id] ? this.mailDetailsMap[mail.id].body : 'Email content loading...'),
      attachments: mail.attachments || []
    };
    this.isEmailDetailsOpen = true;
    this.isNotificationsOpen = false;

    // Mark the email as read when opened
    if (mail.isRead === false) {
      mail.isRead = true;
      this.http.put(`http://localhost:5022/api/emails/${mail.id}/read`, { isRead: true }).subscribe({
        next: () => {
          // Re-fetch count or update state locally if needed
        },
        error: (err) => console.warn('Could not mark email as read in backend:', err)
      });
    }
  }

  aiProcessMail() {
    if (!this.selectedMail) return;
    
    this.isExtracting = true;
    this.http.post<any[]>(`http://localhost:5022/api/emails/${this.selectedMail.id}/extract`, {}).subscribe({
      next: (extractedItems) => {
        this.isExtracting = false;
        localStorage.setItem('extractedEmailProducts', JSON.stringify(extractedItems));
        this.isNotificationsOpen = false;
        this.isEmailDetailsOpen = false;
        
        this.router.navigate(['/lead/sales-enquiry/new']).then(() => {
          setTimeout(() => {
            window.dispatchEvent(new Event('extractedEmailProductsLoaded'));
          }, 300);
        });
      },
      error: (err) => {
        this.isExtracting = false;
        alert('Product extraction failed. Fallback to mock data.');
        // Fallback mock data mapping for local demo validation
        const extractedItems = [
          { group: 'ABB', productDescription: 'ABB Contactor A16-30-10 16A 3P', partNumber: 'A16-30-10', make: 'ABB', model: 'A16-30-10', quantity: 4, rate: 1550, mapping: 'Mapped' },
          { group: 'Siemens', productDescription: 'Siemens MCB 32A 3P C Curve', partNumber: '5SL6332-7', make: 'Siemens', model: '5SL6332-7', quantity: 2, rate: 620, mapping: 'Mapped' },
          { group: 'Schneider', productDescription: 'Schneider CRN Enclosure 300×300×200mm', partNumber: 'NSYCRN33200', make: 'Schneider', model: 'NSYCRN33200', quantity: 1, rate: 2800, mapping: 'Mapped' },
          { group: 'Generic', productDescription: 'DIN Rail 35mm Standard 1 Meter', partNumber: 'DIN-35-1M', make: 'Generic', model: 'DIN-35-1M', quantity: 15, rate: 85, mapping: 'Mapped' },
          { group: 'Unmapped', productDescription: 'savio Pulley Savio Timing Belt Pulley 15T', partNumber: '15T', make: '—', model: '15T', quantity: 10, rate: 0, mapping: 'Unmapped' }
        ];
        localStorage.setItem('extractedEmailProducts', JSON.stringify(extractedItems));
        this.isNotificationsOpen = false;
        this.isEmailDetailsOpen = false;
        this.router.navigate(['/lead/sales-enquiry/new']).then(() => {
          setTimeout(() => {
            window.dispatchEvent(new Event('extractedEmailProductsLoaded'));
          }, 300);
        });
      }
    });
  }

  openAttachment(att: any) {
    if (!this.selectedMail) return;
    const isPdf = att.filename?.toLowerCase().endsWith('.pdf');
    if (isPdf) {
      const rawUrl = `http://localhost:5022/api/emails/${this.selectedMail.id}/attachments/${encodeURIComponent(att.filename)}`;
      this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(rawUrl);
      this.pdfFilename = att.filename;
      this.isPdfPreviewOpen = true;
    } else {
      this.handleOpenPreview(att.filename);
    }
  }

  handleOpenPreview(filename: string) {
    if (!this.selectedMail) return;
    this.isPreviewLoading = true;
    this.previewFilename = filename;
    this.isPreviewOpen = true;
    this.previewSheets = [];
    this.activePreviewSheetIdx = 0;

    this.http.get<any>(`http://localhost:5022/api/emails/${this.selectedMail.id}/attachments/${encodeURIComponent(filename)}/preview`).subscribe({
      next: (data) => {
        this.isPreviewLoading = false;
        this.previewSheets = data.sheets || [];
      },
      error: (err) => {
        this.isPreviewLoading = false;
        alert('Failed to parse Excel sheet preview.');
        this.isPreviewOpen = false;
      }
    });
  }

  getExcelColumnLabel(index: number): string {
    let label = '';
    let temp = index;
    while (temp >= 0) {
      label = String.fromCharCode((temp % 26) + 65) + label;
      temp = Math.floor(temp / 26) - 1;
    }
    return label;
  }

  showToast(message: string, type: 'success' | 'error' | 'info') {
    console.log(`showToast called: type=${type}, message=${message}`);
    if (this.toastTimeout) {
      clearTimeout(this.toastTimeout);
    }
    this.toastMessage = message;
    this.toastType = type;
    this.isToastVisible = true;
    this.cdr.detectChanges();
    this.toastTimeout = setTimeout(() => {
      console.log('Toast auto-dismissing (hiding)');
      this.isToastVisible = false;
      this.cdr.detectChanges();
    }, 3500);
  }

  @HostListener('document:click')
  closeDropdowns() {
    this.isUserMenuOpen = false;
    this.isNotificationsOpen = false;
  }
}
