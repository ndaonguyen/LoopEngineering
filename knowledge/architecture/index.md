---
type: Index
title: "Architecture"
description: "Local map of the architecture domain - the two bug-fixing systems, the auth design and its persistence, and where the dependency rules are enforced."
status: current
---

# Architecture

Boundaries and the reasoning behind them: what each part owns, what it may reference, and which
constraints are structural rather than agreed.

**Belongs here** — a boundary, an invariant, a public contract, or a design whose violation would
be a defect. **Does not** — how to operate something ([../runbooks/](../runbooks/index.md)), why
a choice was made ([../decisions/](../decisions/index.md)), or what was built when
([../../docs/README.md](../../docs/README.md)).

## Concepts

- [loop-engine.md](loop-engine.md) — the port/adapter boundary, the constraints encoded as
  **absent** interface methods, the four `Pipeline` stage gates, run invariants, config surface.
- [bug-loop.md](bug-loop.md) — the skills loop: how a tick decides, why state lives on GitHub,
  the guardrails, why the PR opens red, and how to run it.

## Dependency rules

The rule lives in [CLAUDE.md § Dependency rules](../../CLAUDE.md#dependency-rules) because every
task needs it, not only architecture tasks. It is **enforced** — 
[tests/Architecture.Tests](../../tests/Architecture.Tests/ArchitectureTests.cs) parses the
`.csproj` graph and fails the build on any reference outside the allow-list.

Change the rule and the test together, or the rule is a preference again.

---

Full tree: [../index.md](../index.md)
