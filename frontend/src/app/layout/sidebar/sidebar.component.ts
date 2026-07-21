import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CopilotStateService } from '../../services/copilot-state.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.scss']
})
export class SidebarComponent {
  private copilotStateService = inject(CopilotStateService);

  menus: any[] = [
    {
      name: 'UMS',
      icon: 'manage_accounts',
      isOpen: false,
      children: [
        { name: 'My Profile', link: '/profile', icon: 'person' },
        { name: 'Users', link: '/ums/users', icon: 'people' }
      ]
    },
    {
      name: 'Admin',
      icon: 'admin_panel_settings',
      isOpen: false,
      children: [
        { name: 'Settings', link: '/admin/settings', icon: 'settings' }
      ]
    },
    {
      name: 'Master',
      icon: 'storage',
      isOpen: false,
      children: [
        { name: 'Common', link: '/master/common', icon: 'list' },
        { name: 'Finance', link: '/master/finance', icon: 'account_balance' },
        { name: 'Sales', link: '/master/sales', icon: 'point_of_sale' },
        { name: 'Stores', link: '/master/stores', icon: 'warehouse' }
      ]
    },
    {
      name: 'Lead',
      icon: 'accessibility_new',
      isOpen: true,
      children: [
        {
          name: 'Sales Enquiry',
          icon: 'assignment',
          isOpen: true,
          children: [
            { name: 'Enquiry', link: '/lead/sales-enquiry', icon: 'assignment' }
          ]
        },
        {
          name: 'Quotation',
          icon: 'request_quote',
          isOpen: true,
          children: [
            { name: 'Quotation', link: '/lead/quotation', icon: 'request_quote' },
            { name: 'Quote Approval', link: '/lead/quote-approval', icon: 'verified' }
          ]
        },
        {
          name: 'Sales Order',
          icon: 'shopping_bag',
          isOpen: false,
          children: [
            { name: 'Sales Order', link: '/sales-order/list', icon: 'receipt_long' },
            { name: 'Sales Order Approval', link: '/sales-order/approval', icon: 'approval' },
            { name: 'SO - Accounts Approval', link: '/sales-order/accounts-approval', icon: 'account_balance_wallet' }
          ]
        },
        {
          name: 'Purchase',
          icon: 'shopping_cart',
          isOpen: false,
          children: [
            { name: 'Purchase Order', link: '/purchase/order', icon: 'receipt_long' }
          ]
        },
        {
          name: 'Sales',
          icon: 'local_shipping',
          isOpen: false,
          children: [
            { name: 'Invoice', link: '/sales/invoice', icon: 'description' }
          ]
        },
        {
          name: 'Replacement',
          icon: 'swap_horiz',
          isOpen: false,
          children: [
            { name: 'Replacement List', link: '/replacement/list', icon: 'list' }
          ]
        }
      ]
    },
    {
      name: 'Finance',
      icon: 'account_balance',
      isOpen: false,
      children: [
        { name: 'General Ledger', link: '/finance/ledger', icon: 'menu_book' }
      ]
    },
    {
      name: 'Production',
      icon: 'factory',
      isOpen: false,
      children: [
        { name: 'Work Order', link: '/production/work-order', icon: 'build' }
      ]
    },
    {
      name: 'HRMS',
      icon: 'badge',
      isOpen: false,
      children: [
        { name: 'Employees', link: '/hrms/employees', icon: 'people' }
      ]
    },
    {
      name: 'AI Assistant',
      icon: 'smart_toy',
      link: '/ai-assistant',
      isOpen: false,
      children: []
    }
  ];

  toggleMenu(menu: any) {
    if (menu.children && menu.children.length > 0) {
      menu.isOpen = !menu.isOpen;
    }
  }

  toggleSubMenu(child: any, event: Event) {
    event.stopPropagation();
    if (child.children && child.children.length > 0) {
      child.isOpen = !child.isOpen;
    }
  }

  toggleCopilot(event: Event) {
    event.preventDefault();
    event.stopPropagation();
    this.copilotStateService.toggle();
  }

  isCopilotOpen(): boolean {
    return this.copilotStateService.isOpen();
  }
}
