# Documentation

This directory contains the project documentation for the Personal Finance App.

## Documents

| Document | Description |
|---|---|
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | Start here — clone, configure, run, verify, and the pull request checklist. |
| [CODING_STANDARDS.md](CODING_STANDARDS.md) | Full coding standards — architecture, data layer, EF Core, security, UI, JavaScript/ESLint, versioning and changelog rules. The short version is in the root `AGENTS.md`. |
| [AI_ASSISTANT_SETUP.md](AI_ASSISTANT_SETUP.md) | AI Assistant setup guide — OpenAI API key configuration, cost estimation, troubleshooting, and architecture overview for the chat widget feature. |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Architecture guide — projects, dependencies, request flow, how identity reaches ApiService, and the checklist for adding a feature. |
| [DIAGRAMS.md](DIAGRAMS.md) | Diagram catalogue — runtime context, Feature anatomy and map, data model (ER), request flow, identity and RBAC, account lifecycle, budget data source, Notifications, Research pipeline, Unavailable State, AI Assistant, Google Drive import, startup pipelines, scheduled work. |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Developer guide — local setup, secrets management, EF Core migrations, OAuth configuration, troubleshooting, and detailed architecture. |
| [deployment.md](deployment.md) | Deployment guide — Azure Container Apps, Docker, CI/CD pipelines, Infrastructure as Code (Bicep), and operational runbook. |
| [CUSTOM_DOMAIN.md](CUSTOM_DOMAIN.md) | Custom domain runbook — buying an App Service Domain, Azure DNS records, Container Apps managed certificate, and keeping the binding across CI deploys. |
| [IDENTITY_SCHEMA.md](IDENTITY_SCHEMA.md) | ASP.NET Core Identity database schema — tables, foreign keys, and relational model documentation. |
| [specs/localized-api-errors.md](specs/localized-api-errors.md) | Implementation spec — ApiService rejections carry a localization key and typed arguments (`ApiErrorDto`), one exception filter produces them, Web translates them through the localizer; three slices, Admin excluded. Decided in #385; ADR 0017. |
| [specs/localization-and-multi-currency.md](specs/localization-and-multi-currency.md) | Implementation spec — JSON localization (English + Vietnamese), Azure Translator automation, culture-driven formatting, per-amount currency (USD/VND) with a Reporting Currency, exchange rates, and the PR slices. Decided on Wayfinder map #335; ADRs 0015 and 0016. |
