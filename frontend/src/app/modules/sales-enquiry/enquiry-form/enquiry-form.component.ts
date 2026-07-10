import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ApiService } from '../../../services/api.service';
import { Customer, Agent, Product, SalesEnquiry, EnquiryProduct } from '../../../models/erp.models';

@Component({
  selector: 'app-enquiry-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './enquiry-form.component.html',
  styleUrls: ['./enquiry-form.component.scss']
})
export class EnquiryFormComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  // Mode
  isEditMode = false;
  enquiryId: number | null = null;

  // Masters
  customers: Customer[] = [];
  agents: Agent[] = [];
  products: Product[] = [];

  // Tab State
  activeTab = 'general'; // 'general' | 'products' | 'contacts' | 'ocr'

  // Form State
  enquiry: SalesEnquiry = {
    enquiryNumber: 'Auto-generated on Save',
    enquiryDate: new Date().toISOString().split('T')[0],
    customerId: 0,
    agentId: undefined,
    source: 'Agent',
    leadType: 'Cold',
    address: '',
    assignToId: undefined,
    expiryDate: undefined,
    customerCountry: 'India',
    remarks: '',
    enquiryProducts: []
  };

  // New Item State (for product modal)
  showProductModal = false;
  searchQuery = '';
  searchResults: Product[] = [];
  selectedProductsForForm: any[] = [];

  // OCR state
  ocrFile: File | null = null;
  isOcrProcessing = false;
  ocrResult = '';

  ngOnInit() {
    this.loadMasters();
    this.checkRoute();
  }

  loadMasters() {
    this.api.getCustomers().subscribe(data => this.customers = data);
    this.api.getAgents().subscribe(data => this.agents = data);
    this.api.getProducts().subscribe(data => this.products = data);
  }

  checkRoute() {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.enquiryId = +idParam;
      this.api.getEnquiry(this.enquiryId).subscribe(data => {
        // Strip timestamps from dates for form inputs
        if (data.enquiryDate) {
          data.enquiryDate = data.enquiryDate.split('T')[0];
        }
        if (data.expiryDate) {
          data.expiryDate = data.expiryDate.split('T')[0];
        }
        this.enquiry = data;
      });
    }
  }

  onCustomerChange() {
    const cust = this.customers.find(c => c.id === +this.enquiry.customerId);
    if (cust) {
      this.enquiry.address = cust.address + ', ' + cust.city + ', ' + cust.state;
      this.enquiry.customerCountry = cust.country;
    } else {
      this.enquiry.address = '';
      this.enquiry.customerCountry = 'India';
    }
  }

  selectTab(tab: string) {
    this.activeTab = tab;
  }

  // Product Management
  openAddProductModal() {
    this.showProductModal = true;
    this.searchQuery = '';
    this.searchResults = [];
    this.selectedProductsForForm = [];
  }

  onSearchQueryChange() {
    const q = this.searchQuery.toLowerCase().trim();
    if (q.length < 3) {
      this.searchResults = [];
      return;
    }
    
    this.searchResults = this.products.filter(p => 
      (p.description && p.description.toLowerCase().includes(q)) ||
      (p.partNumber && p.partNumber.toLowerCase().includes(q)) ||
      (p.group && p.group.toLowerCase().includes(q)) ||
      (p.make && p.make.toLowerCase().includes(q))
    );
  }

  clearSearch() {
    this.searchQuery = '';
    this.searchResults = [];
  }

  selectProduct(product: Product) {
    if (this.selectedProductsForForm.some(p => p.id === product.id)) {
      this.clearSearch();
      return;
    }
    
    const formItem = {
      id: product.id,
      group: product.group || '',
      productDescription: product.description || '',
      partNumber: product.partNumber || '',
      make: product.make || '',
      model: product.model || '',
      quantity: 1,
      rate: product.rate || 0,
      uom: 'NOS',
      makes: this.getDistinctMakes(product.group),
      models: this.getDistinctModels(product.group)
    };
    
    this.selectedProductsForForm.push(formItem);
    this.clearSearch();
  }

  addCustomProduct() {
    const customItem = {
      id: 0,
      group: '',
      productDescription: '',
      partNumber: '',
      make: '',
      model: '',
      quantity: 1,
      rate: 0,
      uom: 'NOS',
      makes: this.getDistinctMakes(),
      models: this.getDistinctModels()
    };
    this.selectedProductsForForm.push(customItem);
  }

  removeSelectedFormItem(index: number) {
    this.selectedProductsForForm.splice(index, 1);
  }

  addSelectedProducts() {
    for (const item of this.selectedProductsForForm) {
      this.enquiry.enquiryProducts.push({
        productId: item.id > 0 ? item.id : undefined,
        group: item.group,
        productDescription: item.productDescription,
        partNumber: item.partNumber,
        make: item.make,
        model: item.model,
        quantity: item.quantity,
        rate: item.rate
      });
    }
    this.showProductModal = false;
  }

  removeProductItem(index: number) {
    this.enquiry.enquiryProducts.splice(index, 1);
  }

  getDistinctMakes(group?: string): string[] {
    const list = group 
      ? this.products.filter(p => p.group === group) 
      : this.products;
    const makes = list.map(p => p.make).filter(m => m && m.trim() !== '');
    return Array.from(new Set(makes));
  }

  getDistinctModels(group?: string): string[] {
    const list = group 
      ? this.products.filter(p => p.group === group) 
      : this.products;
    const models = list.map(p => p.model).filter(m => m && m.trim() !== '');
    return Array.from(new Set(models));
  }

  // OCR Upload simulation
  onOcrFileChange(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.ocrFile = file;
    }
  }

  runOcrScan() {
    if (!this.ocrFile) return;
    this.isOcrProcessing = true;
    this.ocrResult = 'Scanning file...';
    setTimeout(() => {
      this.isOcrProcessing = false;
      this.ocrResult = 'OCR Scan complete! Loaded 2 products into Product List.';
      
      // Auto populate two raw cotton/yarn products
      const yarn = this.products.find(p => p.group === 'Yarn');
      const fabric = this.products.find(p => p.group === 'Fabric');
      
      if (yarn) {
        this.enquiry.enquiryProducts.push({
          productId: yarn.id,
          group: yarn.group,
          productDescription: yarn.description,
          partNumber: yarn.partNumber,
          make: yarn.make,
          model: yarn.model,
          quantity: 2500,
          rate: yarn.rate
        });
      }
      if (fabric) {
        this.enquiry.enquiryProducts.push({
          productId: fabric.id,
          group: fabric.group,
          productDescription: fabric.description,
          partNumber: fabric.partNumber,
          make: fabric.make,
          model: fabric.model,
          quantity: 800,
          rate: fabric.rate
        });
      }
    }, 2000);
  }

  // Form submission
  saveEnquiry() {
    if (this.isEditMode && this.enquiryId) {
      this.api.updateEnquiry(this.enquiryId, this.enquiry).subscribe(() => {
        this.router.navigate(['/lead/sales-enquiry']);
      });
    } else {
      this.api.postEnquiry(this.enquiry).subscribe(() => {
        this.router.navigate(['/lead/sales-enquiry']);
      });
    }
  }

  cancel() {
    this.router.navigate(['/lead/sales-enquiry']);
  }
}
