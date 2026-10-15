# Design It Twice

When the user wants to explore alternative interfaces for a chosen deepening candidate, use this parallel sub-agent
pattern. Based on "Design It Twice" (Ousterhout): your first idea is unlikely to be the best.

## Process

### 1. Frame the problem space

Before spawning sub-agents, write a user-facing explanation of the problem space:

- The constraints any new interface would need to satisfy
- The dependencies it would rely on
- A rough illustrative code sketch to ground the constraints

### 2. Spawn sub-agents

Spawn 3+ sub-agents in parallel. Each must produce a **radically different** interface for the deepened module. Give
each agent a different design constraint:

- Agent 1: "Minimize the interface: aim for 1–3 entry points max."
- Agent 2: "Maximise flexibility: support many use cases and extension."
- Agent 3: "Optimise for the most common caller: make the default case trivial."

Each sub-agent outputs:

1. Interface (types, methods, params, plus invariants, ordering, error modes)
2. Usage example
3. What the implementation hides behind the seam
4. Trade-offs

### 3. Present and compare

Present designs sequentially, then compare them in prose. Contrast by **depth**, **locality**, and **seam placement**.
Give your own recommendation.
