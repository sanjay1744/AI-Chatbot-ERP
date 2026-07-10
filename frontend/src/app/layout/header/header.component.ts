import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.scss']
})
export class HeaderComponent {
  userName = 'Thalaimalai';
  userRole = 'Naren Marketing';
  isUserMenuOpen = false;
  isNotificationsOpen = false;
  activeNotifTab = 'mails';

  mockMails = [
    {
      id: 1,
      sender: 'Sanjay S',
      subject: 'enquiry',
      snippet: 'test email ...',
      time: 'Jul 9 03:51 PM'
    },
    {
      id: 2,
      sender: 'Uma Maheshwari',
      subject: 'Fwd: RFQ for Spares List (PDF Attachment)',
      snippet: '---------- Forwarded message ---------- Dear Team, Plea...',
      time: 'Jul 8 05:58 PM'
    },
    {
      id: 3,
      sender: 'Augustine Cruzmuthu',
      subject: 'Enquiry for items',
      snippet: 'give me quote for this ...',
      time: 'Jul 8 04:43 PM'
    },
    {
      id: 4,
      sender: 'Augustine Cruzmuthu',
      subject: 'Give me Enquiry data',
      snippet: 'dsvrkjbvjhznbx ...',
      time: 'Jul 8 04:39 PM'
    }
  ];

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
    alert('Gmail Sync initiated! Checking for new enquiry emails...');
  }

  @HostListener('document:click')
  closeDropdowns() {
    this.isUserMenuOpen = false;
    this.isNotificationsOpen = false;
  }
}
