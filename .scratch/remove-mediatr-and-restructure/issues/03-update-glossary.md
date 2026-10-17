# 03: Update GLOSSARY.md to remove CQRS terms

**What to build:** GLOSSARY.md reflects the actual vocabulary used in the codebase. The Command, Query, and Handler entries are removed. The ApiService layer description is updated to match reality: direct service injection with type-based folder organization. Developers onboarding to the project will see accurate terms.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Remove the `Command` entry from GLOSSARY.md
- [ ] Remove the `Query` entry from GLOSSARY.md
- [ ] Remove the `Handler` entry from GLOSSARY.md
- [ ] Update the `ApiService Layer` entry to describe direct service injection
- [ ] Update the `Feature` entry to reflect type-based organization
- [ ] Keep all other entries (Service, DTO, Entity, AuditableEntity, Soft Delete, domain terms)
