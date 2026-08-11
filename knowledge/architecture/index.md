---
type: Index
title: "Architecture"
description: "Map of the architecture domain - project dependency rules and their enforcement, the two bug-fixing systems, and the auth design plus its persistence."
status: current
---

# Architecture

Boundaries and the reasoning behind them. Load the one your task touches.

## Concepts

| Concept | Read it when you need | Status |
|---|---|---|
| [Loop.Engine](loop-engine.md) | The port/adapter boundary and which project may reference what; the constraints encoded as **absent** interface methods (`ICoder` has no repository path, `IGitPublisher` has no merge) and why they are structural rather than prompted; the four `Pipeline` stage gates and why two of the orderings are load-bearing; run invariants; the full config surface. | current |
| [The skills bug loop](bug-loop.md) | How a tick decides between `start` / `resume` / `wait` / `escalate` / `idle`; why all state lives on GitHub and what the join keys are; the guardrails; why the PR opens red. The prompt-driven counterpart to Loop.Engine — the two coexist deliberately. | current |
| [Authentication](authentication.md) | Token shapes and claims, TTLs, cookie flags and scoping, refresh rotation and reuse detection, threat model, the path to OIDC/SSO. Its section numbers (§) are what the gap register refers to. | current |
| [Database and auth implementation](database.md) | EF Core + Npgsql wiring, the Identity tables, migration commands and who owns the schema, dev auto-migrate versus deploy-step migration, the concrete auth classes and endpoint list, switching provider. | current |
| [Auth gap register](gap.md) | Where the auth **code diverges from** the spec above, each gap classified security / behavioural / schema / by-design. Read this before trusting the spec on any specific claim. Also the worked example of how to report a doc-versus-code conflict. | living |

## Dependency rules

The rule itself lives in [CLAUDE.md](../../CLAUDE.md#dependency-rules) because every task needs
it, not only architecture tasks. Both stacks point inwards:

```
LoopEngineering.Domain ← Application ← Infrastructure ← Api
Loop.Engine.Core       ← Agents, GitHub ← Worker ← Loop.Engine (host)
```

**It is enforced, not merely documented.**
[tests/Architecture.Tests](../../tests/Architecture.Tests/ArchitectureTests.cs) parses the
`.csproj` files and fails the build on any reference outside the allow-list. It holds no project
references of its own, so it cannot distort the graph it checks. Adding a project under
`source/` fails that suite until someone places it deliberately.

Change the rule and the test together, or the rule is a preference again.
