import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ApiService } from '../../../services/api.service';
import { Customer } from '../../../models/erp.models';

@Component({
  selector: 'app-quotation-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './quotation-list.component.html',
  styleUrls: ['./quotation-list.component.scss']
})
export class QuotationListComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  // Filter properties
  status = 'Pending';
  fromDate = '';
  toDate = '';
  selectedCustomerId: number | null = null;
  gridFilter = '';

  // Data properties
  quotations: any[] = [];
  customers: Customer[] = [];
  recordsCount = 0;

  ngOnInit() {
    this.loadMasters();
    this.loadQuotations();
  }

  loadMasters() {
    this.api.getCustomers().subscribe(data => {
      this.customers = data;
      this.cdr.detectChanges();
    });
  }

  loadQuotations() {
    const params: any = {
      status: this.status,
      fromDate: this.fromDate || undefined,
      toDate: this.toDate || undefined,
      customerId: this.selectedCustomerId || undefined,
      query: this.gridFilter || undefined
    };

    this.api.getQuotations(params).subscribe(data => {
      this.quotations = data;
      this.recordsCount = data.length;
      this.cdr.detectChanges();
    });
  }

  onSearch() {
    this.loadQuotations();
  }

  addNew() {
    this.router.navigate(['/lead/quotation/new']);
  }

  editQuotation(id: number) {
    this.router.navigate(['/lead/quotation/edit', id]);
  }

  deleteQuotation(id: number) {
    if (confirm('Are you sure you want to delete this quotation?')) {
      this.api.deleteQuotation(id).subscribe(() => {
        this.loadQuotations();
      });
    }
  }

  viewQuotation(id: number) {
    alert(`Opening View Mode for Quotation ID: ${id}`);
  }

  printQuotation(id: number) {
    alert(`Generating PDF Print layout for Quotation ID: ${id}`);
  }

  logFollowUp(id: number) {
    const notes = prompt('Enter follow-up details (e.g. customer promised approval next week):');
    if (notes) {
      alert(`Follow-up logged successfully: "${notes}"`);
    }
  }
}
