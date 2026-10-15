---
name: azure-deployment
description: Work on Azure Container Apps, Bicep infrastructure, GitHub Actions, Aspire deployment wiring, containers, or production configuration for PersonalFinanceApp.
---

# Azure and Aspire Deployment

Use the repository's documented deployment path: .NET 10 applications, Azure Container Apps Consumption, Azure SQL, GHCR
images, Bicep infrastructure, and GitHub Actions with OIDC.

## Before changing deployment

1. Inspect `docs/deployment.md`, `infra/`, `azure.yaml`, the workflow files, Dockerfiles, and the relevant project
   configuration.
2. Identify whether the change affects the Web container, API container, shared libraries, health probes, service
   discovery, database, secrets, or CI permissions.
3. Preserve environment separation and existing secret injection. Never hardcode credentials, disable TLS/firewall
   protections, or replace OIDC with stored passwords.

## Safe implementation rules

- Keep `PersonalFinance.AppHost` for local orchestration; Azure infrastructure replaces it in production.
- Preserve `/health` and `/alive` behavior and verify probe paths and ports after changes.
- Keep container images non-root and compatible with the existing multi-stage/chiseled image strategy.
- Review Bicep changes for unintended resource replacement, data loss, cost increases, excessive permissions, public
  exposure, and secret leakage.
- Do not reset Azure SQL, drop production resources, or apply production migrations without explicit authorization.
- Use dry-run, What-If, validation, or a dedicated development environment before deployment whenever available.
- Ensure CI still restores, builds, tests, publishes images, deploys, and smoke-tests in the documented order.

## Verification

Run the narrowest relevant validation first, then the documented build/test or infrastructure validation. Report exact
commands and results, and distinguish local validation from an actual Azure deployment. If credentials, subscription
access, or external services are unavailable, state that limitation instead of claiming deployment success.