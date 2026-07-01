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
