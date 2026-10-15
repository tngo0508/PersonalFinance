---
name: retro
description: "Conduct a retrospective on a coding session."
disable-model-invocation: true
---

The user has asked for a **retrospective**. You are suggesting improvements to the coding agent's **environment** to
improve future runs.

## Steps

1. Read the primary sources for the session the user specifies. If the user doesn't specify a session, default to the
   current one.

2. Look for candidates for improvement in these categories:

- **Navigation**: how easy was it for the agent to find the right files? Would a **navigation pointer** make it easier?
- **Automated checks**: are there automated checks that could catch errors the agent made? Linting, typing, tests?
- **Coding standards**: should the reviewer agent be given a new rule to enforce? Should an existing rule be clarified?
- **Global AGENTS.md**: are there any steering instructions that should be moved to coding standards or automated
  checks?
- **Tool economy**: did the agent make expensive tool calls that could be streamlined?
- **No-ops**: look for instructions in steering files that don't modify the agent's behavior.
- **Information access**: look for opportunities to increase the agent's access to information.

3. Present these candidates to the user, in order of severity.

## Reference

### Implementation vs Review

The implementation agent has the most **context pressure**. The review agent has the least—it receives a diff, so no
exploration needed. The review agent should be responsible for imposing coding standards, not the implementation agent.

### Files

- `AGENTS.md`: pushed to the context window of any agent. Use sparingly for **navigation pointers**.
- Docs: use as reference files, pointed to by other files.
- Skills: use for docs (since their description goes into the agent's context window).
