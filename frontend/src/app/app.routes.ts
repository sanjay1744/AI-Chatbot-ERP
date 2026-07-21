import { Routes } from '@angular/router';
import { EnquiryListComponent } from './modules/sales-enquiry/enquiry-list/enquiry-list.component';
import { EnquiryFormComponent } from './modules/sales-enquiry/enquiry-form/enquiry-form.component';
import { QuotationListComponent } from './modules/quotation/quotation-list/quotation-list.component';
import { QuotationFormComponent } from './modules/quotation/quotation-form/quotation-form.component';
import { DashboardComponent } from './modules/dashboard/dashboard.component';
import { LoginComponent } from './modules/auth/login/login.component';
import { EmailSettingsComponent } from './modules/settings/email-settings/email-settings.component';
import { ProfileComponent } from './modules/profile/profile.component';
import { authGuard } from './services/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'profile', component: ProfileComponent },
      { path: 'ums/profile', component: ProfileComponent },
      { path: 'lead/sales-enquiry', component: EnquiryListComponent },
      { path: 'lead/sales-enquiry/new', component: EnquiryFormComponent },
      { path: 'lead/sales-enquiry/edit/:id', component: EnquiryFormComponent },
      { path: 'lead/quotation', component: QuotationListComponent },
      { path: 'lead/quotation/new', component: QuotationFormComponent },
      { path: 'lead/quotation/edit/:id', component: QuotationFormComponent },
      { path: 'settings/email', component: EmailSettingsComponent },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
