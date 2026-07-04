import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Customer, Agent, Product, SalesEnquiry, Quotation } from '../models/erp.models';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private http = inject(HttpClient);
  private baseUrl = 'https://ai-chatbot-erp.onrender.com/api';

  // Master Lists
  getCustomers(): Observable<Customer[]> {
    return this.http.get<Customer[]>(`${this.baseUrl}/master/customers`);
  }

  getAgents(): Observable<Agent[]> {
    return this.http.get<Agent[]>(`${this.baseUrl}/master/agents`);
  }

  getProducts(): Observable<Product[]> {
    return this.http.get<Product[]>(`${this.baseUrl}/master/products`);
  }

  // Sales Enquiries
  getEnquiries(params?: { status?: string; fromDate?: string; toDate?: string; customerId?: number; query?: string }): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/salesenquiries`, { params: params as any });
  }

  getEnquiry(id: number): Observable<SalesEnquiry> {
    return this.http.get<SalesEnquiry>(`${this.baseUrl}/salesenquiries/${id}`);
  }

  createEnquiry(enquiry: SalesEnquiry): Observable<SalesEnquiry> {
    return this.http.get<SalesEnquiry>(`${this.baseUrl}/master/customers`).pipe(
      // Ensure we hit POST correctly
    );
  }

  postEnquiry(enquiry: SalesEnquiry): Observable<SalesEnquiry> {
    return this.http.post<SalesEnquiry>(`${this.baseUrl}/salesenquiries`, enquiry);
  }

  updateEnquiry(id: number, enquiry: SalesEnquiry): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/salesenquiries/${id}`, enquiry);
  }

  deleteEnquiry(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/salesenquiries/${id}`);
  }

  // Quotations
  getQuotations(params?: { status?: string; fromDate?: string; toDate?: string; customerId?: number; query?: string }): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/quotations`, { params: params as any });
  }

  getQuotation(id: number): Observable<Quotation> {
    return this.http.get<Quotation>(`${this.baseUrl}/quotations/${id}`);
  }

  postQuotation(quotation: Quotation): Observable<Quotation> {
    return this.http.post<Quotation>(`${this.baseUrl}/quotations`, quotation);
  }

  updateQuotation(id: number, quotation: Quotation): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/quotations/${id}`, quotation);
  }

  deleteQuotation(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/quotations/${id}`);
  }

  // AI Chatbot
  getChatSessions(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/chat/sessions`);
  }

  getChatSession(id: number): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/chat/sessions/${id}`);
  }

  createChatSession(title: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/chat/sessions`, { title });
  }

  renameChatSession(id: number, title: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/chat/sessions/${id}`, { title });
  }

  deleteChatSession(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/chat/sessions/${id}`);
  }

  postChatMessage(message: string, sessionId: number | null): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/chat`, { message, sessionId });
  }
}
