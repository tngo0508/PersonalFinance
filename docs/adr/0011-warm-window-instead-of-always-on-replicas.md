# 0011 – Production Stays Warm 08:00–22:00 Pacific, Scales to Zero Overnight

**Date:** 2026-10-24  
**Status:** Accepted

## Context

The Web and the ApiService run on Azure Container Apps Consumption with `minReplicas: 0`, which kept their compute
inside the monthly free grant ($0). The cost was cold starts: each app scaled to zero after about five idle minutes,
and waking one took about 25 seconds. The Web woke about 135 times a week. When a page the Web serves needed a
sleeping ApiService, the ApiService woke only after the Web called it, so the Web's HTTP client (three 10-second
attempts, 30-second total) often gave up first. The Dashboard after sign-in then rendered empty or in the Unavailable
State after 30 seconds, 36 times in 14 days. The database was not the cause: Azure SQL Basic is always on and peaked
at 1% DTU.

## Decision

1. **Production keeps one replica of both the Web and the ApiService warm from 08:00 to 22:00 Pacific, every day,** via
   the KEDA cron rule that `enableScheduledScaling` adds. The window covers when the app is used: wake-ups clustered
   between 10:00 and 21:00 Pacific, with the peak at 19:00–20:00.
2. **The window is set in `America/Los_Angeles`, not UTC,** through the `businessHoursTimeZone` deployment parameter,
   so it follows daylight saving. Other environments default to UTC.
3. **Outside the window both apps still scale to zero,** and `minReplicas` stays 0.
4. **The window is deployment configuration, not code:** `deploy-azure.yml` sets it, and
   `enableScheduledScaling=false` returns to pure scale-to-zero with one redeploy.

## Considered Options

- **Keep pure scale-to-zero:** rejected. It costs nothing, but a visitor's first page often took 30 seconds and showed
  no data.
- **`minReplicas: 1` on both apps, warm 24/7:** rejected for now. It removes every cold start, but costs about $10–34 a
  month in Container Apps against $5–18 for the window, and overnight traffic is a handful of requests a week.
- **Warm only the ApiService:** rejected. It fixes the empty Dashboard, but the Web's own 25-second cold start still
  greets the first visitor.
- **Code-only fix (the Web wakes the ApiService when it starts, or a longer Web-to-ApiService timeout):** not chosen
  here. It works around the clock at no cost, but the visitor still waits for both cold starts; it can be added on top
  of the window.

## Consequences

- Container Apps compute leaves the free grant: about $5–18 a month, nearer the low end while the warm replicas mostly
  sit idle (Azure bills a replica at the idle rate below 0.01 vCPU and 1,000 bytes/s received).
- Visitors between 22:00 and 08:00 Pacific still hit cold starts, and can still see the empty Dashboard.
- Scheduled jobs must not rely on the ApiService being awake: the window is configuration and can be switched off. The
  daily Purge and Research refresh stay Container Apps Jobs (ADR 0005).

## Related Decisions

- [0005 – Account Purge Hard-Deletes After a 30-Day Grace Period](0005-account-purge-hard-deletes-after-grace-period.md)
