import { Component, OnInit, inject, HostListener } from '@angular/core';
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

  // Custom Dropdowns State
  isCustomerDropdownOpen = false;
  isSourceDropdownOpen = false;
  isLeadTypeDropdownOpen = false;
  customerSearchQuery = '';

  // New Customer Modal State
  showNewCustomerModal = false;
  newCustomerForm = {
    name: '',
    address: '',
    city: 'Coimbatore',
    state: 'Tamil Nadu',
    country: 'India'
  };

  sourceOptions = ['Agent', 'Direct', 'Email', 'Phone', 'WebSite'];
  leadTypeOptions = ['Select', 'Hot', 'Warm', 'Cold'];

  openNewCustomerModal() {
    this.showNewCustomerModal = true;
    this.newCustomerForm = {
      name: '',
      address: '',
      city: 'Coimbatore',
      state: 'Tamil Nadu',
      country: 'India'
    };
  }

  closeNewCustomerModal() {
    this.showNewCustomerModal = false;
  }

  saveNewCustomer() {
    if (!this.newCustomerForm.name.trim()) return;

    const newCust: Customer = {
      id: 0,
      name: this.newCustomerForm.name.trim(),
      address: this.newCustomerForm.address.trim(),
      city: this.newCustomerForm.city.trim(),
      state: this.newCustomerForm.state.trim(),
      country: this.newCustomerForm.country.trim()
    };

    this.api.createCustomer(newCust).subscribe({
      next: (createdCust) => {
        this.customers.push(createdCust);
        this.customers.sort((a, b) => a.name.localeCompare(b.name));
        this.enquiry.customerId = createdCust.id;
        this.onCustomerChange();
        this.closeNewCustomerModal();
      },
      error: (err) => {
        console.error("Error creating customer:", err);
        alert("Failed to save new customer. Please try again.");
      }
    });
  }

  get selectedCustomerName(): string {
    const cust = this.customers.find(c => c.id === this.enquiry.customerId);
    return cust ? cust.name : '';
  }

  get filteredCustomers(): Customer[] {
    if (!this.customerSearchQuery.trim()) {
      return this.customers;
    }
    const q = this.customerSearchQuery.toLowerCase();
    return this.customers.filter(c => c.name.toLowerCase().includes(q));
  }

  toggleCustomerDropdown() {
    this.isCustomerDropdownOpen = !this.isCustomerDropdownOpen;
    this.isSourceDropdownOpen = false;
    this.isLeadTypeDropdownOpen = false;
    if (this.isCustomerDropdownOpen) {
      this.customerSearchQuery = '';
    }
  }

  selectCustomer(cust: Customer) {
    this.enquiry.customerId = cust.id;
    this.isCustomerDropdownOpen = false;
    this.onCustomerChange();
  }

  clearCustomer(event: Event) {
    event.stopPropagation();
    this.enquiry.customerId = 0;
    this.onCustomerChange();
  }

  toggleSourceDropdown() {
    this.isSourceDropdownOpen = !this.isSourceDropdownOpen;
    this.isCustomerDropdownOpen = false;
    this.isLeadTypeDropdownOpen = false;
  }

  selectSource(src: string) {
    this.enquiry.source = src;
    this.isSourceDropdownOpen = false;
  }

  clearSource(event: Event) {
    event.stopPropagation();
    this.enquiry.source = '';
    this.isSourceDropdownOpen = false;
  }

  toggleLeadTypeDropdown() {
    this.isLeadTypeDropdownOpen = !this.isLeadTypeDropdownOpen;
    this.isCustomerDropdownOpen = false;
    this.isSourceDropdownOpen = false;
  }

  selectLeadType(type: string) {
    this.enquiry.leadType = type === 'Select' ? '' : type;
    this.isLeadTypeDropdownOpen = false;
  }

  clearLeadType(event: Event) {
    event.stopPropagation();
    this.enquiry.leadType = '';
    this.isLeadTypeDropdownOpen = false;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    const target = event.target as HTMLElement;
    if (!target.closest('.custom-dropdown')) {
      this.isCustomerDropdownOpen = false;
      this.isSourceDropdownOpen = false;
      this.isLeadTypeDropdownOpen = false;
    }
  }

  // New Item State (for product modal)
  showProductModal = false;
  searchQuery = '';
  searchResults: Product[] = [];
  selectedProductsForForm: any[] = [];

  // Map Item Modal State
  showMapItemModal = false;
  mappingItem: EnquiryProduct | null = null;
  mappingItemIndex: number | null = null;
  mapSearchQuery = '';
  mapSearchResults: Product[] = [];
  selectedMapProduct: Product | null = null;

  // Contact Persons Tab State
  contacts: any[] = [
    { name: '1111', designation: '1111', phoneHome: '', phoneOffice: '', mobile: '1111111111', email: 'asa@13.com', active: true, isPrimary: false },
    { name: 'Uma Maheshwari', designation: 'ceo', phoneHome: '', phoneOffice: '', mobile: '', email: 'uma.m@ariyaitech.com', active: true, isPrimary: false }
  ];
  filteredContacts: any[] = [];
  searchContactQuery = '';
  showContactModal = false;
  isContactEditMode = false;
  editingContactIndex: number | null = null;
  contactForm: any = {
    name: '',
    designation: '',
    phoneHome: '',
    phoneOffice: '',
    mobile: '',
    email: '',
    active: true,
    isPrimary: false
  };

  // OCR state
  ocrFile: File | null = null;
  isOcrProcessing = false;
  ocrResult = '';

  ngOnInit() {
    this.loadMasters();
    this.checkRoute();
    this.filterContacts();
    this.checkExtractedProducts();
  }

  @HostListener('window:extractedEmailProductsLoaded')
  checkExtractedProducts() {
    const data = localStorage.getItem('extractedEmailProducts');
    if (data) {
      const items = JSON.parse(data);
      this.enquiry.enquiryProducts = items;
      this.activeTab = 'products'; // Switch to Product List tab
      localStorage.removeItem('extractedEmailProducts');
    }

    const emailId = localStorage.getItem('sourceEmailId');
    if (emailId) {
      this.enquiry.sourceEmailId = +emailId;
      this.enquiry.source = 'Email';
      localStorage.removeItem('sourceEmailId');
    }
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

  openMapItemModal(item: EnquiryProduct, index: number) {
    this.showMapItemModal = true;
    this.mappingItem = { ...item };
    this.mappingItemIndex = index;
    this.mapSearchQuery = '';
    this.mapSearchResults = [];
    this.selectedMapProduct = null;
  }

  closeMapItemModal() {
    this.showMapItemModal = false;
    this.mappingItem = null;
    this.mappingItemIndex = null;
    this.mapSearchQuery = '';
    this.mapSearchResults = [];
    this.selectedMapProduct = null;
  }

  onMapSearchQueryChange() {
    const q = this.mapSearchQuery.toLowerCase().trim();
    if (q.length < 3) {
      this.mapSearchResults = [];
      return;
    }
    this.mapSearchResults = this.products.filter(p => 
      (p.description && p.description.toLowerCase().includes(q)) ||
      (p.partNumber && p.partNumber.toLowerCase().includes(q)) ||
      (p.group && p.group.toLowerCase().includes(q)) ||
      (p.make && p.make.toLowerCase().includes(q))
    );
  }

  clearMapSearch() {
    this.mapSearchQuery = '';
    this.mapSearchResults = [];
    this.selectedMapProduct = null;
  }

  selectMapProduct(product: Product) {
    this.selectedMapProduct = product;
    this.mapSearchQuery = product.description;
    this.mapSearchResults = [];
  }

  confirmItemMapping() {
    if (this.mappingItemIndex === null || this.mappingItemIndex === undefined) return;

    if (this.selectedMapProduct) {
      // Situation 1: Mapping to an existing database item
      const item = this.enquiry.enquiryProducts[this.mappingItemIndex];
      item.productId = this.selectedMapProduct.id;
      item.group = this.selectedMapProduct.group;
      item.productDescription = this.selectedMapProduct.description;
      item.partNumber = this.selectedMapProduct.partNumber;
      item.make = this.selectedMapProduct.make;
      item.model = this.selectedMapProduct.model;
      item.rate = this.selectedMapProduct.rate;
      item.mapping = 'Mapped';
      
      this.closeMapItemModal();
    } else {
      // Situation 2: Saving as a Potential Item
      if (!this.mappingItem) return;
      const potItem = {
        name: this.mappingItem.productDescription,
        partNumber: this.mappingItem.partNumber || '',
        rate: this.mappingItem.rate || 0
      };
      this.api.savePotentialItem(potItem).subscribe({
        next: (res) => {
          if (this.mappingItemIndex !== null && this.mappingItemIndex !== undefined) {
            this.enquiry.enquiryProducts[this.mappingItemIndex].mapping = 'Unmapped';
          }
          this.closeMapItemModal();
        },
        error: (err) => {
          console.error("Error saving potential item:", err);
          alert("Failed to save potential item. Please try again.");
        }
      });
    }
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

  // Contacts Management Methods
  filterContacts() {
    const q = this.searchContactQuery.toLowerCase().trim();
    if (!q) {
      this.filteredContacts = [...this.contacts];
      return;
    }
    this.filteredContacts = this.contacts.filter(c => 
      (c.name && c.name.toLowerCase().includes(q)) ||
      (c.designation && c.designation.toLowerCase().includes(q)) ||
      (c.email && c.email.toLowerCase().includes(q)) ||
      (c.mobile && c.mobile.toLowerCase().includes(q))
    );
  }

  openAddContactModal() {
    this.isContactEditMode = false;
    this.editingContactIndex = null;
    this.contactForm = {
      name: '',
      designation: '',
      phoneHome: '',
      phoneOffice: '',
      mobile: '',
      email: '',
      active: true,
      isPrimary: false
    };
    this.showContactModal = true;
  }

  openEditContactModal(idx: number) {
    this.isContactEditMode = true;
    const contact = this.filteredContacts[idx];
    this.editingContactIndex = this.contacts.indexOf(contact);
    this.contactForm = { ...contact };
    this.showContactModal = true;
  }

  saveContact() {
    if (!this.contactForm.name || !this.contactForm.designation || !this.contactForm.mobile || !this.contactForm.email) {
      alert('Please fill out all required fields marked with *');
      return;
    }

    if (this.contactForm.isPrimary) {
      this.contacts.forEach(c => c.isPrimary = false);
    }

    if (this.isContactEditMode && this.editingContactIndex !== null) {
      this.contacts[this.editingContactIndex] = { ...this.contactForm };
    } else {
      this.contacts.push({ ...this.contactForm });
    }

    this.showContactModal = false;
    this.filterContacts();
  }

  deleteContact(idx: number) {
    const contact = this.filteredContacts[idx];
    const mainIdx = this.contacts.indexOf(contact);
    if (mainIdx > -1) {
      this.contacts.splice(mainIdx, 1);
      this.filterContacts();
    }
  }

  // Form submission
  saveEnquiry() {
    if (!this.enquiry.customerId || this.enquiry.customerId === 0) {
      alert("Please select a Customer.");
      return;
    }
    if (!this.enquiry.assignToId) {
      alert("Please select an agent in Assign To.");
      return;
    }
    if (!this.enquiry.enquiryProducts || this.enquiry.enquiryProducts.length === 0) {
      alert("Please add at least one product item in the Product List tab.");
      return;
    }

    if (this.isEditMode && this.enquiryId) {
      this.api.updateEnquiry(this.enquiryId, this.enquiry).subscribe({
        next: () => {
          this.router.navigate(['/lead/sales-enquiry']);
        },
        error: (err) => {
          console.error("Error updating sales enquiry:", err);
          alert("Failed to update sales enquiry. Please check backend server console.");
        }
      });
    } else {
      this.api.postEnquiry(this.enquiry).subscribe({
        next: () => {
          this.router.navigate(['/lead/sales-enquiry']);
        },
        error: (err) => {
          console.error("Error creating sales enquiry:", err);
          alert("Failed to save sales enquiry. Please check backend server console.");
        }
      });
    }
  }

  cancel() {
    this.router.navigate(['/lead/sales-enquiry']);
  }
}
