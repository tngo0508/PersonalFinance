---
name: implement
description: "Implement a piece of work based on a spec or set of tickets."
disable-model-invocation: true
---

Implement the work described by the user in the spec or tickets.

If the user passes a ticket reference, fetch it and state its title before starting. If the reference is ambiguous, ask.

Use the `tdd` skill where possible, at pre-agreed seams.

Run builds and tests regularly. Run the full test suite once at the end.

Once done, use the `code-review` skill to review the work.
