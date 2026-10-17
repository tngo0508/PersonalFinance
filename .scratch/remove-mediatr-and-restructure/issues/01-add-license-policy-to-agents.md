# 01: Add license policy to AGENTS.md

**What to build:** A clear license policy in AGENTS.md requiring only MIT, BSD, or Apache 2.0 licensed NuGet packages, with MediatR and AutoMapper explicitly listed as banned. AI agents and developers will check a package's license before adding it to the project.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Add a "License Policy" section to AGENTS.md
- [ ] State the requirement: only MIT, BSD, or Apache 2.0 licensed packages
- [ ] Explicitly list banned packages: MediatR, AutoMapper
- [ ] Add a rule: before adding a NuGet package, check its license
- [ ] Add a rule: if a package changes its license to require payment, remove it and find an alternative
