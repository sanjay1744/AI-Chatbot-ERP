# Enterprise Git Workflow Specification

This repository follows a structured, production-grade Git branching model designed for high reliability, automated quality gates, and multi-developer collaboration.

---

## 1. Branching Strategy

```text
main (Production)
  ▲
  │ (Protected PRs only)
  │
develop (Staging & Integration)
  ▲
  ├── feature/login-module
  ├── feature/ocr-parser
  └── feature/sales-enquiry-ui
```

### Core Branches
* **`main`**: Long-running production branch. Direct pushes are **strictly forbidden**. Every commit here represents a deployable, production-ready release tagged with semantic versioning (e.g., `v1.0.0`).
* **`develop`**: The integration branch where all completed features merge. Served as the staging build area.

### Supporting Branches
* **`feature/<feature-name>`**: Created off `develop`. Used for new features or refactoring (e.g., `feature/quotation-grid`). Merged back to `develop` via Pull Request.
* **`release/<version>`**: Created off `develop` when preparing for a production deployment (e.g., `release/v1.1.0`). QA testing happens here. Bug fixes are committed here and merged back to both `main` and `develop`.
* **`hotfix/<bug-name>`**: Created off `main` for critical production emergencies. Merged back to both `main` and `develop`.

---

## 2. Commit Message Conventions (Conventional Commits)

All commits must follow the Conventional Commits specification to enable automated release notes:

* `feat:` A new feature (`feat: add multi-currency quotation calculation`)
* `fix:` A bug fix (`fix: resolve OCR table extraction misalignment`)
* `docs:` Documentation updates (`docs: update Swagger API specs`)
* `style:` Formatting changes (`style: format Angular SCSS tokens`)
* `refactor:` Code restructuring without functional changes
* `test:` Adding or updating unit/integration tests
* `chore:` Maintenance tasks (`chore: upgrade .NET SDK to 9.0`)

---

## 3. Pull Request & Code Review Process

1. **Never merge your own code.** All Pull Requests require at least **1 senior developer approval**.
2. **CI Pipeline Validation.** All GitHub Actions workflows (`Code Quality`, `Build & Tests`, `Security Scan`) must pass before merging is allowed.
3. **Squash & Merge / Rebase.** Keep commit history clean when merging features into `develop`.
