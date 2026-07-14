import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ApiService } from '../../../services/api.service';
import { Customer } from '../../../models/erp.models';

@Component({
  selector: 'app-enquiry-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './enquiry-list.component.html',
  styleUrls: ['./enquiry-list.component.scss']
})
export class EnquiryListComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  // Filter properties
  enquiryType = 'Pending';
  fromDate = '';
  toDate = '';
  selectedCustomerId: number | null = null;
  gridFilter = '';

  // Data properties
  enquiries: any[] = [];
  customers: Customer[] = [];
  recordsCount = 0;

  ngOnInit() {
    this.loadMasters();
    this.loadEnquiries();
  }

  loadMasters() {
    this.api.getCustomers().subscribe(data => this.customers = data);
  }

  loadEnquiries() {
    const params: any = {
      status: this.enquiryType,
      fromDate: this.fromDate || undefined,
      toDate: this.toDate || undefined,
      customerId: (this.selectedCustomerId && this.selectedCustomerId.toString() !== 'null') ? Number(this.selectedCustomerId) : undefined,
      query: this.gridFilter || undefined
    };

    this.api.getEnquiries(params).subscribe({
      next: (data) => {
        this.enquiries = data;
        this.recordsCount = data.length;
      },
      error: (err) => {
        console.error('Error loading enquiries:', err);
      }
    });
  }

  onSearch() {
    this.loadEnquiries();
  }

  addNew() {
    this.router.navigate(['/lead/sales-enquiry/new']);
  }

  viewEnquiry(enq: any) {
    this.router.navigate(['/lead/sales-enquiry/edit', enq.id]);
  }

  deleteEnquiry(id: number) {
    if (confirm('Are you sure you want to delete this sales enquiry?')) {
      this.api.deleteEnquiry(id).subscribe({
        next: () => {
          this.loadEnquiries();
        },
        error: (err) => {
          console.error('Error deleting enquiry:', err);
          alert('Failed to delete sales enquiry.');
        }
      });
    }
  }

  convertToQuotation(enq: any) {
    this.router.navigate(['/lead/quotation/new'], { queryParams: { enquiryId: enq.id } });
  }
}
