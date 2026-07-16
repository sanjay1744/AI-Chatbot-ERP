# AI-Chatbot-ERP

[![CI/CD Pipeline](https://github.com/sanjay1744/AI-Chatbot-ERP/actions/workflows/ci.yml/badge.svg)](https://github.com/sanjay1744/AI-Chatbot-ERP/actions/workflows/ci.yml)
[![Enterprise Git Workflow](https://img.shields.io/badge/Git%20Workflow-MNC%20Standard-blue)](docs/GIT_WORKFLOW.md)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Enterprise-grade **ERP & CRM Software** for Manufacturing Companies, built with **Angular 21**, **ASP.NET Core 9 (.NET)**, and **SQL**.

---

## Repository Structure & Governance

This repository adheres to standard **MNC Software Development Lifecycle (SDLC)** governance:

```text
AI-Chatbot-ERP/
├── .github/
│   ├── workflows/ci.yml          # Automated CI/CD build, test & security pipeline
│   └── PULL_REQUEST_TEMPLATE.md  # Mandatory peer code review checklist
├── docs/
│   ├── ARCHITECTURE.md           # System design & technology stack specification
│   └── GIT_WORKFLOW.md           # Branching model & commit conventions
├── .gitignore                    # Enterprise ignore rules for .NET, Angular, Node & OS
└── README.md
```

## Branching & Release Pipeline

We enforce strict branch isolation and protected workflows:
* **`main`**: Protected production branch. Deployable state only.
* **`develop`**: Protected integration branch. All feature branches merge here via Pull Request.
* **`feature/*`**: Individual developer feature branches created off `develop`.

For full details on commit conventions and pull request rules, see our [Git Workflow Documentation](docs/GIT_WORKFLOW.md).

---

## Quickstart

### 1. Clone & Setup Branch
```bash
git clone https://github.com/sanjay1744/AI-Chatbot-ERP.git
cd AI-Chatbot-ERP
git checkout develop
```

### 2. Start a New Feature
```bash
git checkout -b feature/my-new-feature
```

---

## Running the Application

Follow the steps below to set up and run both the backend and frontend services on your machine.

### Prerequisites

* **.NET 9 SDK**: Required to compile and run the backend Web API.
* **Node.js**: Required to install dependencies and run the Angular application.
* **Ollama**: Download and run [Ollama](https://ollama.com/) locally to power the chatbot, then download the default model:
  ```bash
  ollama pull qwen2.5-coder:1.5b
  ```
  *(Note: You can customize the model name by updating the `OLLAMA_MODEL` variable in the root [.env](file:///d:/AriyAI/chatbot_/.env) file or in the backend's [appsettings.json](file:///d:/AriyAI/chatbot_/backend/AriyAI.ERP.Api/appsettings.json)).*

### 1. Start the Backend (.NET Core Web API)

The backend uses EF Core with SQLite. The database file (`erp.db`) will be automatically created, migrated, and seeded with mock manufacturing data upon application startup.

1. Navigate to the API directory:
   ```bash
   cd backend/AriyAI.ERP.Api
   ```
2. Run the project:
   ```bash
   dotnet run
   ```
   *For hot-reloads during development, you can use:*
   ```bash
   dotnet watch
   ```
3. The API will be available at:
   - HTTP: `http://localhost:5022`
   - HTTPS: `https://localhost:7298`

### 2. Start the Frontend (Angular 21)

1. Navigate to the frontend directory:
   ```bash
   cd frontend
   ```
2. Install npm dependencies:
   ```bash
   npm install
   ```
3. Run the development server:
   ```bash
   npm start
   ```
4. Open your browser and navigate to:
   - `http://localhost:4200/`

