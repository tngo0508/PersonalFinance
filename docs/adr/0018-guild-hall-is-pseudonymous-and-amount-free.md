# 0018 – The Guild Hall Is Pseudonymous and Never Shows Money Amounts

**Date:** 2026-10-26  
**Status:** Accepted

## Context

The Community page was a test feature: made-up stories with invented authors and like counts, site-wide stat tiles, and
posts shown under the author's display name. Money is a sensitive subject, and a page that shows names, amounts or
rankings invites comparing net worth, which makes people anxious and puts them off rather than helping them. Raised in
#396.

## Decision

The page becomes the **Guild Hall**, a place to trade practical Strategies and keep up habits together, not to show off.

1. **Every Account appears under a generated Pseudonym.** Real names and emails never appear. An Account can re-roll its
   Pseudonym, and its past posts then show the new one.
2. **No money amounts and no comparison.** No leaderboards, no stat tiles, no Challenge about an amount of money. The
   post form flags currency amounts while the user types and asks them to describe the change instead; percentages and
   counts of days are allowed. Admins reject Strategies that flex.
3. **Accountability stays private.** A Check-in is visible only to its Account. The Guild Hall shows only how many
   people checked in, never who did or how anyone is doing.
4. **Feedback is supportive only.** Reactions are Helpful and Cheering you on; there is no downvote, and Comments show no
   counts.
5. **Strategies are approved by an Admin before others see them**; Comments are shown at once and an Admin can hide them.

## Considered Options

- **Display name, with an optional "post anonymously".** Rejected: users would be identifiable by default, and a mix of
  named and anonymous posts makes the anonymous ones stand out.
- **Anonymous per post.** Rejected: with no stable identity, people can't recognise each other over time, so
  accountability goes.
- **Anonymous benchmarks ("your savings rate vs. people like you").** Rejected for this page: it is exactly the comparison
  the Guild Hall exists to avoid.
- **A hard block on currency amounts.** Rejected: it feels hostile and is easy to get around; a nudge plus review is
  firm enough.

## Consequences

- Adding a figure, total or ranking to the Guild Hall later reverses this ADR and needs a new one.
- When an Account is Purged, its Strategies and Comments stay, shown as "Former member"; its Check-ins, votes and
  Reactions are deleted.
