---
type: Index
title: "Knowledge index"
description: "The map for finding context in this repo - which document answers which kind of question, and how far each can be trusted."
status: current
---

# Knowledge index

The map for finding context. Load what your task's scope names — not the whole tree.
Working rules for using this index are in [CLAUDE.md](../CLAUDE.md).

**Status** means how far you can trust a document on its own:

| | |
|---|---|
| **current** | Describes intended behaviour today. Still verify against code before relying on it. |
| **living** | Updated as work happens; expect it to change under you. |
| **historical** | Explains *why*, not *what is*. Never cite it as current behaviour. |

---

## The template — Clean-Architecture .NET service

| Document | Read it when you need | Status |
|---|---|---|
| [../README.md](../README.md) | Build/test/run commands, dev vs prod-like SPA modes, curl recipes for the auth flow, deploy skeleton. **Its "Use it as a `dotnet new` template" section describes packaging that does not exist** — see the conflict note in [CLAUDE.md](../CLAUDE.md). | current, one known conflict |
| [database.md](database.md) | EF Core + Npgsql wiring, `AppDbContext` and the Identity tables, migration commands and who owns the schema, dev auto-migrate/seed vs deploy-step migration, the concrete auth implementation (`AuthCookies`, `TokenService`, `ICurrentUser`, endpoint list, RBAC opt-in, lockout), local Postgres, switching provider. | current |
| [authentication.md](authentication.md) | The auth *design spec* — token shapes and claims, TTLs, cookie flags and scoping, refresh rotation and reuse detection, threat model, the path to OIDC/SSO. Section numbers (§) here are what `gap.md` refers to. | current |
| [gap.md](gap.md) | Where the auth code diverges from that spec, each gap classified security / behavioural / schema / by-design, with the accepted-vs-open call recorded. Also the worked example of how to report a doc-vs-code conflict. | living |

## Loop.Engine — the autonomous bug-fixing engineer

| Document | Read it when you need | Status |
|---|---|---|
| [loop-engine.md](loop-engine.md) | The port/adapter boundary and what each project may reference, the constraints encoded as *absent* interface methods, the `Pipeline` stage gates (`GenerateFix` → `ReproduceFirst` → `VerifyFix` → `PublishPr`) and why two of the orderings are load-bearing, the run invariants, the full config surface, and where to look when changing a given thing. **Start here.** | current |
| [bug-loop-roadmap.md](bug-loop-roadmap.md) | What each delivery phase was *for* and its exit criterion, the agent-per-responsibility architecture sketch, and what the first real runs exposed. The phase records are the reasoning behind the current pipeline shape. | living |
| [bug-loop-proposal.md](bug-loop-proposal.md) | The externally-sourced multi-agent design this was compared against. **Explicitly not adopted** — it describes nothing that runs. | historical |

## The skills bug loop

| Document | Read it when you need | Status |
|---|---|---|
| [bug-loop.md](bug-loop.md) | How a tick decides (`start` / `resume` / `wait` / `escalate` / `idle`), why all state lives on GitHub and what the join keys are, the guardrails, the cloud-routine schedule and env-var config, and the design rationale (why the PR opens red, why `fix/`, why `gh`). | current |
| [bug-loop-learnings.md](bug-loop-learnings.md) | Cross-issue debugging memory — non-obvious test seams, plausible-but-wrong hypotheses, errors that point away from their cause. Read before diagnosing; append after fixing, only if it would have saved time. Not a changelog. | living |

## Process artifacts

| Document | Read it when you need | Status |
|---|---|---|
| [inputs/TEMPLATE.md](inputs/TEMPLATE.md) | The brief format `/plan-issue` expects — issue restatement, design links, brainstorming, constraints and non-goals. Copy it to start a new one. | current |
| `inputs/3-6-*.md` | The briefs that produced the shipped phase plans — constraints and ruled-out approaches, as they stood at the time. | historical |
| `plans/3-6-*.md` | Implementation plans for phases already merged. Useful for the reasoning behind a decision, useless as a description of the code. | historical |

> The file numbers in `plans/` and `inputs/` are **issue** numbers, and the phase numbering is
> offset by one: issue #3 delivered Phase 2, #6 delivered Phase 5.

---

## Not covered here

Skills under `.claude/skills/` are their own progressive-disclosure layer — each `SKILL.md` is
the entry point and loads its own leaf documents. Do not index them here; invoke the skill.
