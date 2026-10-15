---
name: codebase-design
description: Shared vocabulary for designing deep modules. Use when the user wants to design or improve a module's interface, find deepening opportunities, decide where a seam goes, make code more testable or AI-navigable, or when another skill needs the deep-module vocabulary.
---

# Codebase Design

Design **deep modules**: a lot of behaviour behind a small interface, placed at a clean seam, testable through that
interface. Use this language and these principles wherever code is being designed or restructured. The aim is leverage
for callers, locality for maintainers, and testability for everyone.

## Glossary

Use these terms exactly: don't substitute "component," "service," "API," or "boundary."

**Module**: anything with an interface and an implementation. A function, class, package, or tier-spanning slice.
_Avoid_: unit, component, service.

**Interface**: everything a caller must know to use the module correctly: the type signature, but also invariants,
ordering constraints, error modes, required configuration, and performance characteristics. _Avoid_: API, signature.

**Implementation**: what's inside a module, its body of code.

**Depth**: leverage at the interface. A module is **deep** when a large amount of behaviour sits behind a small
interface, **shallow** when the interface is nearly as complex as the implementation.

**Seam** _(Michael Feathers)_: a place where you can alter behaviour without editing in that place. _Avoid_: boundary
(overloaded with DDD's bounded context).

**Adapter**: a concrete thing that satisfies an interface at a seam.

**Leverage**: what callers get from depth. More capability per unit of interface they learn.

**Locality**: what maintainers get from depth. Change, bugs, and verification concentrate in one place.

## Principles

- **Depth is a property of the interface, not the implementation.** A deep module can be internally composed of small,
  mockable parts.
- **The deletion test.** Imagine deleting the module. If complexity vanishes, it was a pass-through. If complexity
  reappears across N callers, it was earning its keep.
- **The interface is the test surface.** Callers and tests cross the same seam.
- **One adapter means a hypothetical seam. Two adapters means a real one.** Don't introduce a seam unless something
  actually varies across it.

## Designing for testability

1. **Accept dependencies, don't create them.** Inject external dependencies.
2. **Return results, don't produce side effects.** Prefer pure functions.
3. **Small surface area.** Fewer methods = fewer tests needed. Fewer params = simpler test setup.

## Going deeper

- **Deepening a cluster given its dependencies**, see [DEEPENING.md](DEEPENING.md)
- **Exploring alternative interfaces**, see [DESIGN-IT-TWICE.md](DESIGN-IT-TWICE.md)
