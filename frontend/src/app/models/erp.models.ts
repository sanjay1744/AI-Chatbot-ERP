export interface Customer {
  id: number;
  name: string;
  address: string;
  city: string;
  state: string;
  country: string;
}

export interface Agent {
  id: number;
  name: string;
  email: string;
  phone: string;
}

export interface Product {
  id: number;
  group: string;
  description: string;
  partNumber: string;
  make: string;
  model: string;
  rate: number;
}

export interface SalesEnquiry {
  id?: number;
  enquiryNumber?: string;
  enquiryDate?: string;
  customerId: number;
  customer?: Customer;
  agentId?: number;
  agent?: Agent;
  source: string;
  leadType: string;
  address: string;
  assignToId?: number;
  assignedAgent?: Agent;
  expiryDate?: string;
  customerCountry: string;
  remarks: string;
  status?: string;
  aging?: number;
  sourceEmailId?: number;
  enquiryProducts: EnquiryProduct[];
  itemsCount?: number;
}

export interface EnquiryProduct {
  id?: number;
  salesEnquiryId?: number;
  productId?: number;
  group: string;
  productDescription: string;
  partNumber: string;
  make: string;
  model: string;
  quantity: number;
  rate: number;
  mapping?: string;
}

export interface Quotation {
  id?: number;
  quotationNumber?: string;
  quotationDate?: string;
  customerReference: string;
  currency: string;
  dueDate: string;
  customerId: number;
  customer?: Customer;
  address: string;
  agentId?: number;
  agent?: Agent;
  subject1: string;
  subject2: string;
  status?: string;
  aging?: number;
  quotationProducts: QuotationProduct[];
  itemsCount?: number;
}

export interface QuotationProduct {
  id?: number;
  quotationId?: number;
  productId?: number;
  group: string;
  productDescription: string;
  partNumber: string;
  make: string;
  model: string;
  quantity: number;
  rate: number;
}

export interface PotentialItem {
  id?: number;
  name: string;
  partNumber: string;
  rate: number;
}
