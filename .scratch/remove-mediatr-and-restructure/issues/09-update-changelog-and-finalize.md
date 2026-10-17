# 09: Update CHANGELOG and finalize

**What to build:** CHANGELOG.md documents the architectural alignment under `[Unreleased]`. The application version is bumped in `AppVersion.cs`. A final cross-check confirms no orphaned CQRS or MediatR references remain in documentation files (ADR-0001, GLOSSARY.md, AGENTS.md). The project is left in a clean, consistent state.

**Blocked by:** 08: Remove old feature-based folders and verify (contract phase).

**Status:** ready-for-agent

- [ ] Add entry to CHANGELOG.md under `## [Unreleased]` → `### Changed`
- [ ] Describe the architectural alignment: removed aspirational CQRS/MediatR references, documented direct service injection, added license policy
- [ ] Bump version in `PersonalFinance.Shared/Constants/AppVersion.cs` (patch bump for documentation alignment)
- [ ] Final cross-check: search for remaining references to "MediatR", "CQRS", "Command", "Query", "Handler" in documentation
- [ ] Verify AGENTS.md Backend/Frontend Code Separation section reflects type-based organization
- [ ] Solution builds with zero errors
