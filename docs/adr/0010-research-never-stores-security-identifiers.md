# 0010 – Research Never Stores or Shows Security Identifiers

**Date:** 2026-10-24  
**Status:** Accepted

## Context

Research reads fund holdings from public SEC Form N-PORT filings, which list each holding's CUSIP and ISIN. A US ISIN
embeds the CUSIP, and CUSIP Global Services licenses CUSIP use and forbids publishing CUSIP data. Its exception for
identifiers found in public sources covers non-commercial use only, and it has not said how that applies to derived
data or third-party mapping services. The fund overlap view needs to know when two funds hold the same security, and
names alone cannot tell two share classes of one issuer apart.

## Decision

1. **CUSIPs and ISINs are used only in memory, during a refresh run,** to match holdings across funds (and, later, to
   company names). The parser keeps a holding's identifier (ISIN, else CUSIP) on the in-memory holding only.
2. **They are never persisted, logged, cached or displayed, and never sent to an outside service.** Snapshots refer to
   a holding by its name, or by its index in the snapshot's list of names; companies are keyed by SEC CIK or by name.
3. **A derived dataset that needs identifiers is recomputed from source.** The fund overlap snapshot is rebuilt from
   each fund's latest N-PORT report whenever any fund has filed a newer one, because there is no stored identifier to
   rebuild it from. It is not kept per quarter.
4. **Holdings are joined to SEC industries by name matching plus a configured override list,** not through OpenFIGI.
5. **A test enforces the rule:** after a refresh over filings with real-shaped CUSIPs and ISINs, no stored payload, API
   response or log message contains a CUSIP- or ISIN-shaped value.

## Considered Options

- **Store the identifiers to key holdings:** rejected. It publishes CUSIP data in the database and the page source,
  which the CUSIP licence forbids outside its non-commercial exception, and leaves the derived-data question open.
- **Store a hash of each identifier:** rejected. A CUSIP has about a billion possible values with a check digit, so a
  hash is easily reversed and is the identifier in all but name.
- **Map holdings through OpenFIGI:** deferred. It is an external dependency with its own terms. The app is
  non-commercial, so it could close the gap later, under the same in-memory rule.
- **Match holdings by name only:** rejected for overlap. Two share classes of one issuer (e.g. Alphabet's) have the
  same name and would merge.

## Consequences

- The overlap shows each fund's latest report only, and a refresh after a new filing downloads each fund's latest
  report again.
- About four points of US-fund weight cannot be classified by industry. It shows as "Not classified by the SEC", never
  as a wrong industry.
- One licensing question and one external dependency are removed.
