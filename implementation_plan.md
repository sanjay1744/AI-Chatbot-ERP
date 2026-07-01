# Recreating AriyAI ERP & CRM Manufacturing Software (Angular, .NET 9, SQL)

This plan outlines the end-to-end development of the **AriyAI ERP & CRM Software for Manufacturing**, faithfully recreating and enhancing the interface and functionality shown in the provided screenshots using a modern enterprise tech stack: **Angular 21**, **ASP.NET Core 9 Web API (.NET C#)**, and **SQL Database (via Entity Framework Core)**.

## User Review Required

> [!IMPORTANT]
> **Database Selection**: By default, the .NET Web API will be configured with **Entity Framework Core using SQLite** so that the application can be run immediately on your machine (`dotnet run`) without requiring you to install or configure SQL Server credentials. The database layer uses clean EF Core entities and migrations, meaning you can switch to Microsoft SQL Server at any time simply by changing the connection string in `appsettings.json` and switching the provider.

> [!NOTE]
> **Workspace Location**: The project will be created in a clean folder inside your workspace: `d:\AriyAI\AriyAI-ERP` with `backend/` (.NET 9 Web API) and `frontend/` (Angular 21 application).

## Open Questions

- No critical open questions. The screenshots provide a comprehensive visual specification of the sidebar navigation, header, Sales Enquiry module (list and tabbed forms), and Quotation module (list and tabbed forms).

---

## Proposed Architecture & Directory Structure

```
d:\AriyAI\AriyAI-ERP\
├── backend\
│   └── AriyAI.ERP.Api\
│       ├── Controllers\         # API Endpoints (SalesEnquiriesController, QuotationsController, MasterController)
│       ├── Data\                # ErpDbContext & Database Seeders
│       ├── Models\              # Entities (SalesEnquiry, EnquiryProduct, Quotation, QuotationProduct, Customer, Agent)
│       ├── DTOs\                # Request/Response Data Transfer Objects
│       ├── Program.cs           # Dependency Injection, CORS, Swagger, EF Core setup
│       └── appsettings.json     # Database Connection configuration
└── frontend\
    └── ariyai-erp-ui\
        ├── src\
        │   ├── app\
        │   │   ├── layout\      # Top Header, Left Navigation Sidebar (collapsible ERP menu)
        │   │   ├── modules\
        │   │   │   ├── sales-enquiry\  # List View, New Enquiry Form (Tabs: General Info, Product List, Contacts, Image OCR)
        │   │   │   └── quotation\      # Quotations List View, New Quotation Form (7 tabs)
        │   │   ├── services\    # ApiService connecting Angular to .NET API
        │   │   └── models\      # TypeScript interfaces matching backend models
        │   └── styles.scss      # Custom design system matching AriyAI visual aesthetics
```

---

## Detailed Module Breakdown

### 1. Backend: ASP.NET Core 9 Web API + SQL (EF Core)
- **Data Entities**:
  - `Customer`: Code, Name, City, State, Country, Address.
  - `Agent`: Code, Name, Email, Phone.
  - `Product`: PartNumber, Description, Group, Make, Model, UnitPrice.
  - `SalesEnquiry`: EnquiryNumber, EnquiryDate, CustomerId, AgentId, Source, LeadType, Status, ExpiryDate, Remarks, ItemsCount, AgingDays, EnquiryProducts list.
  - `EnquiryProduct`: EnquiryId, PartNumber, Description, Quantity, Rate.
  - `Quotation`: QuotationNumber, QuotationDate, CustomerId, AgentId, Status, Currency, DueDate, Subject1, Subject2, ItemsCount, AgingDays, QuotationProducts list.
- **Seeded Manufacturing Data**:
  - Exact records matching your screenshots (`Sri Manjunatha Spinning Mills Ltd`, `Pasupati Spinning & Weaving Mills`, `S.P.APPARELS LTD`, `SUMANLAL J. SHAH & CO`, agents `AJITH`, `U. THALAIMALAI`, `N.JAYAPRAKASH`, etc.).
- **API Features**:
  - Complete CRUD operations.
  - Filtering by Date Range, Customer, Type/Status, and keyword search.

### 2. Frontend: Angular 21 Application
- **Layout & Navigation**:
  - **Sidebar Menu**: Replicates the exact hierarchical navigation (`UMS`, `Admin`, `Master`, `Lead -> Sales Enquiry / Quotation`, `Sales Order`, `Purchase`, `Sales`, `Replacement`, `Finance`, `Production`, `HRMS`).
  - **Top Header**: Toggle button, menu search box, notifications icon, and logged-in user profile (`Thalaimalai / Naren-Marketing`).
- **Lead -> Sales Enquiry**:
  - **List Page**: Filter panel (Enquiry Type dropdown, From/To dates, Customer select, Grid filter), export buttons, and data grid displaying status badges and aging indicators in red.
  - **New Enquiry Form**: 4 tabs (`General Info`, `Product List`, `Contacts`, `Image OCR`). Includes product table with modal/inline item insertion.
- **Lead -> Quotation**:
  - **List Page**: Status filter (`Pending`), search panel, data table with inline actions (`Edit`, `Delete`, `View`, `Print`, `Follow-Up`).
  - **New Quotation Form**: 7 structured tabs (`General Info`, `Quotation Product`, `Terms & Condition`, `Email(s) to Send Quotation`, `Charges`, `Delivery Address`, `Export Info`).

---

## Verification Plan

### Automated Tests & Build Verification
- **Backend**: Run `dotnet build` and test API endpoints using `dotnet run` / Swagger.
- **Frontend**: Run `ng build` to ensure zero compilation errors or TypeScript type mismatches.

### End-to-End Execution
- Start the .NET Web API server on port 5000/5001.
- Start the Angular development server (`ng serve`) and verify all pages, navigation links, filters, and form submissions work seamlessly.
