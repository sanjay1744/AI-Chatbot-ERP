# Technical Architecture Specification

## Overview
AI-Chatbot-ERP is an enterprise-grade ERP & CRM platform tailored for manufacturing companies. It integrates sales enquiries, dynamic quotations, order processing, and intelligent OCR extraction.

## Technology Stack
* **Frontend**: Angular 21 (TypeScript, Standalone Components, Reactive Forms, Responsive Enterprise UI).
* **Backend**: ASP.NET Core 9 Web API (.NET C#).
* **Database**: SQL (Entity Framework Core with clean relational migrations).
* **DevOps**: GitHub Actions CI/CD Pipeline, Docker containerization support.

## Core Modules
1. **Lead & Enquiry Management**: Multi-channel lead tracking, aging calculations, and item counts.
2. **Quotation & Approval Engine**: Dynamic calculation of product rates, taxes, and structured approval workflows.
3. **Intelligent Document OCR**: Automated extraction of tabular product data from customer inquiry documents.
