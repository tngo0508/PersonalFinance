# How other goal apps show pace

Research for [#391](https://github.com/tngo0508/PersonalFinance/issues/391), part of the map
[#388 Wayfinder: Savings Goals upgrade](https://github.com/tngo0508/PersonalFinance/issues/388).
Sources read on 2026-10-25. The surveyed apps can change their behaviour, so recheck a source before
relying on a detail.

**Question.** How do established personal-finance apps tell a user whether a savings goal is on track?
For each app: the on-track / behind / ahead rule, how "needed per month" is computed, how a finish
date is projected, how goals without a target date are handled, and how pace is shown on screen and in
notifications.

**Method.** Only first-party sources: official help-centre articles, the vendor's own API spec, and the
vendor's own product pages. Where a vendor documents nothing on a point, this note says so rather than
filling the gap from reviews or forums. "Not documented" means the official docs are silent. It does
not mean the feature is missing from the app.

---

## 1. YNAB (Targets)

Sources:
[How to Use Targets](https://support.ynab.com/how-to-use-targets-rk5kkI9ks) ·
[Underfunded logic](https://support.ynab.com/en_us/underfunded-a-guide-BJwPhQO09) ·
[Edit Plan / Cost to Be Me](https://support.ynab.com/plan-and-adjust-with-edit-plan-and-cost-to-be-me-ByR7vpqPyx) ·
[Progress bars](https://support.ynab.com/en_us/progress-bars-a-guide-SkDEhot09) ·
[Snooze a target](https://support.ynab.com/snooze-a-target-HyS4E5rZT) ·
[YNAB API OpenAPI spec](https://api.ynab.com/papi/open_api_spec.yaml) (`Category` schema, `goal_*` fields)

- **Model.** A target sits on a budget category. It has a cadence (weekly, monthly, yearly, or custom)
  and a behaviour ("Set aside another…", "Refill up to…", and for custom targets "Have a balance of…").
  The API names the savings shapes `TB` (Target Category Balance) and `TBD` (Target Category Balance
  by Date). (API spec, `goal_type`.)
- **On-track rule.** Pace is a per-month funding check, not a forecast. The API defines
  `goal_under_funded` as "the amount of funding still needed in the current month to stay on track
  towards completing the goal within the current goal period". Companion fields:
  `goal_months_to_budget` (months left in the period, including the current one), `goal_overall_funded`,
  `goal_overall_left` and `goal_percentage_complete`. A category is either funded for the month (green)
  or underfunded (yellow). There is no "ahead" status. Overfunding is surfaced only as an Auto-Assign
  action, "Reduce Overfunding". (API spec; Underfunded article.)
- **Needed per month.** For yearly and custom targets that span more than one month, the amount already
  funded is part of the calculation, so the monthly ask goes down as you fund the target and up if you
  unassign money "to stay on track" (Edit Plan, "Next Month's Targets"). Taken with
  `goal_months_to_budget`, this points to *remaining amount ÷ remaining months*, recomputed every month.
  That formula is an **inference**: the docs never state it.
- **Projected finish date.** Not documented. YNAB asks for a required rate and never projects a date
  from past deposits.
- **No target date.** A "Have a Balance" target with no due date is ignored by Underfunded: "Since there
  are no specific dates affiliated with these categories, you'll need to assign funds to these categories
  yourself" (Underfunded article). It still has a target amount and a percentage complete.
- **Snooze.** Snoozing a target stops the monthly ask for the current month only. The yellow warning
  turns green or grey and shows a "Zz" icon (Snooze article).
- **Visual.** Yellow Available amount when underfunded. A donut chart in category Details "lets you know
  your progress toward completing your target, and whether or not you're on track". Optional progress
  bars per category: green = funded or snoozed, yellow = assigned but target not met, grey = nothing
  assigned, red = overspent (Targets article; Progress bars article).
- **Notifications.** None documented for targets. Signals stay inside the plan screen.

## 2. Monarch Money (Save up goals, "Goals 3.0")

Sources:
[Using Save Up Goals](https://help.monarch.com/hc/en-us/articles/44373182867476-Using-Save-Up-Goals)
(updated 2026-10-19) ·
[Goals in Monarch](https://help.monarch.com/hc/en-us/articles/44373110771860-Goals-in-Monarch) ·
[Growth Rates in Save Up Goals](https://help.monarch.com/hc/en-us/articles/47822256713236-Growth-Rates-in-Save-Up-Goals)

- **Model.** A goal has an optional Target amount, an optional Target date, and a Planned monthly
  contribution. Money reaches a goal through dated "fund allocations" (contribution or withdrawal, with a
  past or future date) or through linked income and transfer transactions. A per-goal toggle sets
  whether spending reduces progress.
- **On-track rule.** Four statuses: **On track** ("pacing to reach your goal by your target date"),
  **Ahead** ("expected to reach your goal early"), **At risk** ("You'll miss your goal unless you adjust
  your plan"), and **Complete**. "Statuses only appear when your goal has a target amount, date, and
  planned monthly contribution."
- **Projection basis.** The projection runs forward from the current balance plus the *planned* monthly
  contribution. It is not based on past deposits: "Adjust your monthly contribution or target date
  anytime to see your projection update instantly." The docs mention no all-time or recent-window
  average. When an account's whole balance funds the goal, the projection compounds an account-level
  annual growth rate (defaults: 0% checking, 3% savings, 7% investment) that is converted to a monthly
  rate.
- **Needed per month.** Monarch shows a *recommended* monthly contribution. With growth rates on, it
  accounts for projected compounding of existing balances and of the other accounts' planned
  contributions (Growth Rates article). Without growth it is presumably remaining ÷ months, but the docs
  do not spell this out.
- **No target date.** Allowed. The goal shows progress and a timeline but no status.
- **Visual.** A timeline chart per goal: a green line for the goal balance, a purple line for the target
  amount, a black line for spending from the goal, and a dotted projection line.
- **Notifications.** Only an alert when goals are over-allocated against the real account balances
  (plus reconciliation alerts). No pace or behind alerts are documented.

## 3. Copilot Money (Goals)

Sources:
[Creating New Goals](https://help.copilot.money/en/articles/11100462-creating-new-goals) ·
[Goals FAQ](https://help.copilot.money/en/articles/11139571-goals-faq) ·
[Savings Goal Tab Overview](https://help.copilot.money/en/articles/11470324-savings-goal-tab-overview) ·
[Updating Goal Progress](https://help.copilot.money/en/articles/11100474-updating-goal-progress)

- **Model.** A goal has a target amount and starting savings. Progress comes from an allocated share of
  account balances (optionally "Always use the entire balance") or from linked transactions. At creation
  you pick **one** tracking method: a **target date** or a **monthly contribution**.
- **Two modes.** With a target date, the app shows "the amount you need to save each month" to finish
  on time, and the last day of the target month is the deadline. With a monthly contribution, the app
  shows how long it will take to reach the target. A goal without a date is therefore the
  "monthly contribution" mode, which projects a duration.
- **On-track rule.** The progress bar is green when "you're on track to complete this goal in time" and
  yellow when "you're a little behind", which "is usually because you didn't contribute enough to the
  goal in previous months". The rule compares what was actually contributed by month with the plan. It
  does not forecast from an average. The exact threshold is not documented, and there is no "ahead"
  state.
- **Projected finish date.** Only in monthly-contribution mode, based on the chosen contribution. The
  docs mention no historical averaging.
- **Visual.** A chart at the top of the Goals tab compares "the amount saved this month and the amount
  needed to meet your savings goals for the month", with a toggle to "All time". Goal states are Active,
  Ready to Spend (set automatically on reaching the target) and Archived. "Reactivate when funds dip"
  moves a goal back to Active.
- **Notifications.** Not documented.

## 4. Qapital (Goals + Rules)

Sources:
[Create Goals](https://help.qapital.com/en/articles/10245417-create-goals) ·
[Edit Goals](https://help.qapital.com/en/articles/10245419-edit-goals) ·
[Completed Goal](https://help.qapital.com/en/articles/10245420-completed-goal) ·
[Set and Forget Rule](https://help.qapital.com/en/articles/10245402-set-forget-rule) ·
[52 Week Rule](https://help.qapital.com/en/articles/10245396-52-week-rule)

- **Model.** A goal has a name, an amount and a cover photo. It is funded by automated **Rules**: fixed
  daily, weekly or monthly amounts (Set and Forget), round-ups, a payday percentage, the 52-week ladder,
  and others. The current Create Goals article does not mention a target date.
- **On-track rule, needed per month, projection.** None is documented in the official help centre.
  Pace is implied by the rules: the user picks a contribution rate rather than being given one.
- **Completion.** On completion Qapital "automatically creates a new Goal titled 'Your next goal'" with
  the same rules.
- **Visual / notifications.** Not documented for pace.

## 5. Banks

Banks document much less than budgeting apps.

**Ally Bank: savings buckets.**
Source: [What are Ally Bank's Savings Buckets and Boosters?](https://www.ally.com/stories/save/what-are-ally-banks-savings-buckets-and-boosters/)
(Ally, Oct 2024).
A bucket has "the target amount you want to save and your goal date" (up to 30 per account), plus
"Boosters": recurring transfers, round-ups and Surprise Savings. Ally shows "a personalized chart that
tracks your progress (and your projected growth)". No on-track rule, needed-per-month formula, behaviour
without a date, or notifications are documented.

**Monzo: Pot goals (US only; Monzo US closed all accounts on 8 June 2026).**
Source: [Monzo Community announcement by the Monzo team, 12 Sep 2023](https://community.monzo.com/t/set-target-dates-on-your-pots-to-help-achieve-your-savings-goals/153248).
"Simply tell us your goal and when you want to achieve it by". Monzo then computes the contribution per
day, week or month and offers to set up an automatic deposit "to keep you on track", with a progress
bar. In the same thread the team said that recalculating the scheduled amount after a manual top-up was
for "a future iteration", so the per-period amount was fixed at setup when announced. This is a weaker
source: a staff forum post for a product that no longer exists. The UK help pages read did not document
goal pace.

**Starling Bank: Spaces** (UK, for contrast).
Source: [Spaces feature page](https://www.starlingbank.com/features/spaces/).
Easy Saver Spaces have an optional savings target and automatic transfers. The page describes no target
date or pace rule.

---

## Comparison

| | Status values | Status needs | Needed per month | Finish-date projection | No target date | Notifications |
|---|---|---|---|---|---|---|
| YNAB | funded / underfunded (no "ahead") | dated target | remaining ÷ months left, re-asked monthly (inferred) | none | no monthly ask; % only | none documented |
| Monarch | on track / ahead / at risk / complete | amount + date + planned contribution | recommended contribution (growth-aware) | forward from **planned** contribution (+ growth) | allowed, no status | over-allocation only |
| Copilot | on track (green) / behind (yellow) | target-date mode | shown in target-date mode | only in monthly-contribution mode, from the chosen amount | means monthly-contribution mode | none documented |
| Qapital | none documented | n/a | n/a (user-set rules) | none documented | date not in create flow | none documented |
| Ally | none documented | n/a | not documented | "projected growth" chart | not documented | none documented |
| Monzo US (defunct) | none documented | goal date | per day/week/month at setup | none | n/a | none documented |

## Synthesis

**Common patterns**

1. **Pace is plan-based, not history-based.** No surveyed vendor documents projecting a finish date
   from an all-time or recent-window average of past deposits. A projection, where one exists, runs
   forward from a *declared* rate (Monarch's planned contribution, Copilot's monthly contribution). A
   status, where one exists, compares actual funding with the required rate (YNAB, Copilot).
2. **Needed per month is recomputed from what is left.** YNAB and Copilot both re-derive the monthly
   ask from the remaining amount and remaining time. Falling behind makes later months ask for more
   ("didn't contribute enough … in previous months"). The fixed alternative, a per-period amount set
   once, appears only in Monzo's 2023 launch, and its team flagged that as something to improve.
3. **A status needs a date.** All three apps with a status hide it without a target date. Undated goals
   fall back to percentage progress, or (Copilot) to "at this monthly amount, it takes N months".
4. **Colour is the main signal.** Green and yellow (YNAB, Copilot) or a four-value text status
   (Monarch), plus a progress bar or donut and, in Monarch, a balance-vs-target timeline with a dotted
   projection.
5. **Notifications are rare.** No vendor documents a "you're behind" push or email. Pace stays inside
   the app.

**Notable differences**

- **Ahead.** Only Monarch has an explicit "Ahead" status. YNAB shows over-funding only as a "Reduce
  Overfunding" action, and Copilot has no ahead state.
- **Inputs required.** Monarch needs three inputs for a status (amount, date and a planned contribution).
  YNAB and Copilot need only amount and date and derive the rate.
- **Date or rate.** Copilot makes the user choose between date-driven and rate-driven tracking. Monarch
  allows both at once and compares them.
- **Withdrawals.** Monarch and Copilot let the user decide whether spending from a goal reduces progress
  (a refillable emergency fund versus a one-off purchase). YNAB counts assigned money over the period.

## Options relevant to this app

The goal has a target amount, an optional target date, and a planned log of dated deposits and
withdrawals. These are options, not recommendations. The app is information-only and never moves money.

- **A. Required-rate check (YNAB/Copilot style).** Needed per month = (target − current) ÷ months left.
  The status compares contributions logged this month, or cumulatively, with a straight-line schedule
  from the start to the target date. It needs only data the goal already has plus the log. Open
  questions: whether to count partial months, and where the schedule starts (goal creation or first
  deposit).
- **B. Declared-rate projection (Monarch style).** Add a "planned monthly contribution" field. Project
  the finish date as current + planned × months, and derive on track / ahead / at risk by comparing that
  date with the target date. This needs a new field, and the plan may drift from the user's real
  behaviour.
- **C. Observed-rate projection (not seen in any surveyed vendor).** Project the finish date from the
  contribution log: net deposits over an all-time window or a recent window (e.g. the last 3 or 6
  months). This is the only option that works for undated goals without a declared rate. Trade-offs: an
  all-time average is stable but slow to react, while a recent window reacts fast but is noisy with
  sparse logs. It needs a minimum history before it shows anything, and a rule for net-negative or zero
  rates ("no projection").
- **D. Undated goals.** Choose among: progress percentage only (YNAB, Monarch); "at X per month you'd
  finish in N months" from a declared rate (Copilot); or an observed-rate estimate (option C).
- **E. Ahead state.** Either show it explicitly (Monarch) or fold it into "on track" (YNAB, Copilot).
- **F. Withdrawals.** Either treat every withdrawal as reducing progress, or add a per-goal "spending
  reduces progress" toggle (Monarch, Copilot).
- **G. Signalling.** Every surveyed vendor keeps pace in the UI (colour, bar, timeline). Notifications
  for pace would go beyond what any of them documents.
