# 09: Assisted actions: which writes, and how users confirm them

Type: prototype
Status: open
Blocked by: 07
Map: [AI Finance Assistant map](../map.md)

## Question

Which state-changing actions may the assistant propose (adjust a budget category amount, create or update a savings
goal, add a Budget Tracker transaction, ...), and how does a proposal become a write? Prototype the confirmation flow
in the chat widget: a proposal card showing the exact change, explicit Confirm/Cancel, an anti-forgery-protected call
through the normal Web -> ApiService endpoint (never a direct write by the model), and an audit trail. Decide whether
an action can be undone.
