import { Routes } from '@angular/router';
import { EnquiryListComponent } from './modules/sales-enquiry/enquiry-list/enquiry-list.component';
import { EnquiryFormComponent } from './modules/sales-enquiry/enquiry-form/enquiry-form.component';
import { QuotationListComponent } from './modules/quotation/quotation-list/quotation-list.component';
import { QuotationFormComponent } from './modules/quotation/quotation-form/quotation-form.component';

export const routes: Routes = [
  { path: 'lead/sales-enquiry', component: EnquiryListComponent },
  { path: 'lead/sales-enquiry/new', component: EnquiryFormComponent },
  { path: 'lead/sales-enquiry/edit/:id', component: EnquiryFormComponent },
  { path: 'lead/quotation', component: QuotationListComponent },
  { path: 'lead/quotation/new', component: QuotationFormComponent },
  { path: 'lead/quotation/edit/:id', component: QuotationFormComponent },
  { path: '', redirectTo: 'lead/sales-enquiry', pathMatch: 'full' },
  { path: '**', redirectTo: 'lead/sales-enquiry' }
];
