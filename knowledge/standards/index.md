---
type: Index
title: "Standards"
description: "Map of the standards domain - conventions a tool cannot check. Everything a tool can check is enforced rather than documented; this index routes to the enforcement and states the bar for adding a document here."
status: current
---

# Standards

**This folder is empty on purpose.**

A standard that a tool can check belongs in the tool, not in a document. An agent that reads a
convention may follow it; a build that rejects a violation leaves no room. Every convention in
this repo that could be automated already has been, so there is nothing left here that a
document would improve.

This index exists to route to that enforcement — and to hold the line on what gets written here.

## Where the standards are enforced

| Convention | Enforced by | Fails at |
|---|---|---|
| Project dependency direction, both stacks | [tests/Architecture.Tests](../../tests/Architecture.Tests/ArchitectureTests.cs) — parses the `.csproj` graph against an allow-list | `dotnet test` |
| Formatting and whitespace of **generated** code | `dotnet format` inside `FixVerifier`, scoped to the files a fix touched. Fixes rather than reports — whitespace is mechanical, so no repair attempt is spent on it | Before the fix is built |
| Formatting of hand-written code | `.editorconfig` | Editor / `dotnet format` |
| Conventional Commits on PR titles | [.github/workflows/pr-lint.yml](../../.github/workflows/pr-lint.yml) | CI |
| Build, test and coverage | [.github/workflows/ci.yaml](../../.github/workflows/ci.yaml) | CI |
| Test framework and assertions | xUnit + AwesomeAssertions, uniform across all four test projects | Convention, visible in every test file |

## The bar for adding a document here

Add one only when a convention is **genuinely uncheckable** and **genuinely non-obvious** — a
rule that a competent engineer reading the surrounding code would not already infer.

Two anti-patterns to refuse:

- **A language style guide.** `dotnet.md` describing C# conventions duplicates `.editorconfig`
  and the analyzers, and goes stale the moment they change. If a rule matters, encode it there.
- **Restating what the code shows.** Naming conventions, folder layout, which class calls which —
  the code states these more precisely and never drifts from itself.

If you find yourself writing a rule here, first ask whether an analyzer, an `.editorconfig`
entry, or a test could carry it instead. That version cannot be ignored under deadline pressure.

## Related

- Repo-wide working rules for agents: [CLAUDE.md](../../CLAUDE.md)
- What each stack may reference: [../architecture/index.md](../architecture/index.md)
