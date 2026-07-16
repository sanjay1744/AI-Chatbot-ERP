import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ApiService } from '../../../services/api.service';
import { Customer, Agent, Product, Quotation, QuotationProduct } from '../../../models/erp.models';

@Component({
  selector: 'app-quotation-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './quotation-form.component.html',
  styleUrls: ['./quotation-form.component.scss']
})
export class QuotationFormComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  // Mode
  isEditMode = false;
  quotationId: number | null = null;

  // Masters
  customers: Customer[] = [];
  agents: Agent[] = [];
  products: Product[] = [];

  // Tab State
  activeTab = 'general'; // 'general' | 'products' | 'terms' | 'emails' | 'charges' | 'delivery' | 'export'

  // Form State
  quotation: Quotation = {
    quotationNumber: 'Auto-generated on Save',
    quotationDate: new Date().toISOString().split('T')[0],
    customerReference: '',
    currency: 'INR',
    dueDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
    customerId: 0,
    address: '',
    agentId: undefined,
    subject1: '',
    subject2: '',
    quotationProducts: []
  };

  // Product Add Modal State
  showProductModal = false;
  selectedProductId: number | null = null;
  newProductQty = 1;
  newProductRate = 0;

  ngOnInit() {
    this.loadMasters();
    this.checkRoute();
  }

  loadMasters() {
    this.api.getCustomers().subscribe(data => {
      this.customers = data;
      this.cdr.detectChanges();
    });
    this.api.getAgents().subscribe(data => {
      this.agents = data;
      this.cdr.detectChanges();
    });
    this.api.getProducts().subscribe(data => {
      this.products = data;
      this.cdr.detectChanges();
    });
  }

  checkRoute() {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.quotationId = +idParam;
      this.api.getQuotation(this.quotationId).subscribe(data => {
        if (data.quotationDate) {
          data.quotationDate = data.quotationDate.split('T')[0];
        }
        if (data.dueDate) {
          data.dueDate = data.dueDate.split('T')[0];
        }
        this.quotation = data;
        this.cdr.detectChanges();
      });
    } else {
      // Check query parameter for conversion from enquiry
      this.route.queryParams.subscribe(params => {
        const enquiryId = params['enquiryId'];
        if (enquiryId) {
          this.api.getEnquiry(+enquiryId).subscribe(enq => {
            this.quotation.customerId = enq.customerId;
            this.quotation.address = enq.address;
            this.quotation.agentId = enq.agentId || enq.assignToId;
            this.quotation.customerReference = enq.enquiryNumber || '';
            this.quotation.subject1 = enq.remarks || '';
            
            // Map products
            if (enq.enquiryProducts) {
              this.quotation.quotationProducts = enq.enquiryProducts.map(ep => ({
                productId: ep.productId,
                group: ep.group || '',
                productDescription: ep.productDescription || ep.partNumber || '',
                partNumber: ep.partNumber || '',
                make: ep.make || '',
                model: ep.model || '',
                quantity: ep.quantity,
                rate: ep.rate
              }));
            }
            this.cdr.detectChanges();
          });
        }
      });
    }
  }

  onCustomerChange() {
    const cust = this.customers.find(c => c.id === +this.quotation.customerId);
    if (cust) {
      this.quotation.address = cust.address + ', ' + cust.city + ', ' + cust.state;
    } else {
      this.quotation.address = '';
    }
  }

  selectTab(tab: string) {
    this.activeTab = tab;
  }

  // Product management
  openAddProductModal() {
    this.showProductModal = true;
    this.selectedProductId = null;
    this.newProductQty = 1;
    this.newProductRate = 0;
  }

  onProductSelect() {
    const prod = this.products.find(p => p.id === Number(this.selectedProductId));
    if (prod) {
      this.newProductRate = prod.rate;
    }
  }

  addProductItem() {
    const prod = this.products.find(p => p.id === Number(this.selectedProductId));
    if (prod) {
      const item: QuotationProduct = {
        productId: prod.id,
        group: prod.group,
        productDescription: prod.description,
        partNumber: prod.partNumber,
        make: prod.make,
        model: prod.model,
        quantity: this.newProductQty,
        rate: this.newProductRate
      };
      this.quotation.quotationProducts.push(item);
      this.showProductModal = false;
    }
  }

  removeProductItem(index: number) {
    this.quotation.quotationProducts.splice(index, 1);
  }

  saveQuotation() {
    if (this.isEditMode && this.quotationId) {
      this.api.updateQuotation(this.quotationId, this.quotation).subscribe(() => {
        this.router.navigate(['/lead/quotation']);
      });
    } else {
      this.api.postQuotation(this.quotation).subscribe(() => {
        this.router.navigate(['/lead/quotation']);
      });
    }
  }

  cancel() {
    this.router.navigate(['/lead/quotation']);
  }
}
