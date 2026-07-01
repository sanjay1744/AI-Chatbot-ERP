import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.scss']
})
export class SidebarComponent {
  menus = [
    { name: 'UMS', icon: 'people', isOpen: false, children: [] as any[] },
    { name: 'Admin', icon: 'settings', isOpen: false, children: [] as any[] },
    {
      name: 'Master',
      icon: 'folder_open',
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
      icon: 'trending_up',
      isOpen: true,
      children: [
        { name: 'Sales Enquiry', link: '/lead/sales-enquiry', icon: 'assignment' },
        { name: 'Quotation', link: '/lead/quotation', icon: 'request_quote' },
        { name: 'Quote Approval', link: '/lead/quote-approval', icon: 'verified' }
      ]
    },
    {
      name: 'Sales Order',
      icon: 'shopping_cart',
      isOpen: false,
      children: [
        { name: 'Sales Order', link: '/sales-order/list', icon: 'receipt_long' },
        { name: 'Sales Order Approval', link: '/sales-order/approval', icon: 'approval' },
        { name: 'SO - Accounts Approval', link: '/sales-order/accounts-approval', icon: 'account_balance_wallet' }
      ]
    },
    { name: 'Purchase', icon: 'shopping_bag', isOpen: false, children: [] as any[] },
    { name: 'Sales', icon: 'storefront', isOpen: false, children: [] as any[] },
    { name: 'Replacement', icon: 'swap_horiz', isOpen: false, children: [] as any[] },
    { name: 'Finance', icon: 'account_balance', isOpen: false, children: [] as any[] },
    { name: 'Production', icon: 'precision_manufacturing', isOpen: false, children: [] as any[] },
    { name: 'HRMS', icon: 'badge', isOpen: false, children: [] as any[] }
  ];

  toggleMenu(menu: any) {
    if (menu.children && menu.children.length > 0) {
      menu.isOpen = !menu.isOpen;
    }
  }
}
