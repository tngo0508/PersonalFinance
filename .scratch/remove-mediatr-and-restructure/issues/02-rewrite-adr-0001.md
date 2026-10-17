# 02: Rewrite ADR-0001 to reject MediatR and document actual architecture

**What to build:** ADR-0001 accurately describes the actual architecture: direct service injection without CQRS or MediatR. It documents why CQRS was rejected (MediatR licensing, unnecessary indirection for project scale) and records the license constraint (MIT/BSD/Apache 2.0 only) as a guiding principle. Future developers and AI agents will understand why the project uses direct service calls.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Rewrite ADR-0001 title and content to reflect the actual decision
- [ ] Document why CQRS with MediatR was rejected (licensing, indirection, simplicity)
- [ ] Document the actual pattern: controllers inject service interfaces directly via DI
- [ ] Add the license constraint (MIT/BSD/Apache 2.0 only) as a core principle
- [ ] Update the folder organization section to describe type-based structure
- [ ] Keep the AuditableEntity and soft-delete decisions (already implemented and correct)
- [ ] Keep the Web layer simplicity decision (no CQRS at Web layer)
