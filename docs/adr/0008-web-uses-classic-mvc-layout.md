# 0008 – Web Uses the Classic MVC Layout

**Date:** 2026-10-23  
**Status:** Accepted (supersedes ADR 0007 §1 and §5 for `PersonalFinance.Web` only)

## Context

ADR 0007 moved Web's controllers, view models and UI services into `Features/<Feature>/` and left Razor views in
`Views/<Controller>/`. After the move (#160) a Web page is split across two trees that use different names: the Budget
Planner's controller and model live in `Features/BudgetPlanner/` but its views in `Views/Budget/`; the Landing Page is
`Features/LandingPage/` and `Views/Home/`; the Watchlist's news feed is `Features/Watchlist/` and `Views/News/`. Model,
view and controller can no longer be found from one another by rule, which was 0007's own reason for feature folders.
A Web Feature is one to four C# files; its behaviour lives in ApiService, where feature folders do pay off.

## Decision

1. **Web uses the layout ASP.NET Core MVC expects.** Every controller lives in `Controllers/`, including JSON-only ones
   and `NotificationController`. View models, request and response shapes and view formatting helpers live in
   `Models/<Controller>/`, named after the controller that renders them, mirroring `Views/<Controller>/`. Web-side UI
   services live in `Services/` and tag helpers in `TagHelpers/`. `Infrastructure/` holds only host plumbing: the
   user-id forwarding handler, Refit client registration, cookie and authorization handlers, email, the ApiService
   warmer and the Unavailable State.
2. **Namespaces follow the folders** (`PersonalFinance.Web.Controllers`, `PersonalFinance.Web.Models.<Controller>`, …),
   and `Views/_ViewImports.cshtml` imports every `Models.*` namespace.
3. **Registrations go back into `Program.cs`.** Web has no `Add<Feature>Feature()` extensions.
4. **A test enforces it.** `Architecture/WebMvcLayoutTests` checks that every Web type sits in an allowed namespace
   (a `Models.<X>` namespace only when there is an `XController`), that every controller is in `Controllers`, and that
   every `Views/<X>/` folder has an `XController`. `Architecture/FeatureLayoutTests` keeps the Feature rules for
   ApiService only.
5. **Unchanged:** ApiService, `Shared` and the test project keep ADR 0007's Feature layout; the Identity area keeps its
   Razor Pages layout; client JavaScript stays in `wwwroot/js/<feature>/`.

A Feature is still the unit of work. In Web it is reached through its controllers, whose names come from their routes;
`docs/ARCHITECTURE.md` maps each Feature to its Web controllers.

## Considered Options

- **Views inside Feature folders** (`Features/<Feature>/Views/`, with a custom `IViewLocationExpander`): rejected again,
  for 0007's reason. It works against the convention every ASP.NET tutorial and tool assumes.
- **Keep the split and rename to line up:** renaming the Web Feature folders to controller names breaks "one Feature
  name in every project" anyway; renaming the controllers changes URLs.

## Consequences

- The move is a mechanical, logic-free PR released as a patch.
- `NewsService` (RSS fetching, parsing and caching) never belonged in Web. The layout move (#170) leaves it in
  `Services/`, and #171 then moves it into ApiService's Watchlist Feature behind a Refit client.
