---
name: writing-for-agents
description: Writing documents for agents. Use when creating or editing skills, or modifying AGENTS.md or CLAUDE.md.
---

Reference for writing any document an agent consumes: a skill, an `AGENTS.md`, or a doc reached by a pointer.

## Context pointers

A **context pointer** is a reference held in the agent's context that names some out-of-context material and encodes the
condition for reaching it. A skill's description is one; a line in `AGENTS.md` naming a doc is the same object.

- **Front-load the leading word**: the pointer is where it does its triggering work.
- **One trigger per branch.** Synonyms that rename a single branch are one branch written twice.
- **Cut identity the body already carries.**

## The two loads

- **Context load** is the cost of always-loaded material on the agent's window.
- **Cognitive load** is the cost on the human: which documents exist and when to reach for each.

## Information hierarchy

1. **In-file step** is the primary tier: what the agent does, in order.
2. **In-file reference** is consulted on demand.
3. **Disclosed reference** is pushed out into a separate file, reached by a context pointer.

**Progressive disclosure** is the move down the ladder so the top stays legible.

## Steps and completion criteria

Every step ends on a **completion criterion**, the condition that tells the agent the work is done. Two properties:

- **Clarity**: can the agent tell done from not-done?
- **Demand**: how much it requires. "Every modified model accounted for" forces thorough work.

## Leading words

A **leading word** is a compact concept already living in the model's pretraining that the agent thinks with while
running the document. Repeated as a token, it accumulates a distributed definition and anchors behavior in the fewest
tokens.

## Pruning

- Keep each meaning in a **single source of truth**.
- The **environment** is a source of truth too (config files, directory layout).
- Check every line for **relevance**.
- Hunt **no-ops**: an instruction the model already obeys by default.
