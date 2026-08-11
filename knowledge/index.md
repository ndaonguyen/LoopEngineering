---
type: Index
title: "Knowledge layer"
description: "The map. Every concept in this repo's knowledge tree, grouped by domain, with enough description to pick the right one without opening it."
status: current
---

# Knowledge layer

How the system is **designed** — what must stay true. What was *done* lives in
[../docs/](../docs/README.md).

Load only what your task names. The descriptions below are the selection signal — read them,
pick, then open.

## [Architecture](architecture/index.md)

- [Loop.Engine](architecture/loop-engine.md) — the port/adapter boundary and which project may
  reference what; the constraints encoded as **absent** interface methods (`ICoder` has no
  repository path, `IGitPublisher` has no merge); the four `Pipeline` stage gates and why two of
  the orderings are load-bearing; run invariants; the config surface.
- [The skills bug loop](architecture/bug-loop.md) — how a tick decides between `start` /
  `resume` / `wait` / `escalate` / `idle`; why all state lives on GitHub and what the join keys
  are; the guardrails; why the PR opens red; and how to run it.
- [Authentication](architecture/authentication.md) — token shapes and claims, TTLs, cookie flags
  and scoping, refresh rotation and reuse detection, threat model, the path to OIDC/SSO. Its §
  numbers are what the gap register cites.
- [Database and auth implementation](architecture/database.md) — EF Core + Npgsql wiring, the
  Identity tables, who owns the schema, migration commands, dev auto-migrate versus deploy-step,
  the concrete auth classes and endpoint list, local Postgres, switching provider.
- [Auth gap register](architecture/gap.md) — where the auth **code diverges from** that spec,
  classified security / behavioural / schema / by-design. Read before trusting the spec on any
  specific claim. Also the worked example of reporting a doc-versus-code conflict.

## [Decisions](decisions/index.md)

- [How decisions are recorded](decisions/how-decisions-are-recorded.md) — why there are no ADR
  files, the three-part bar for writing one, and where each settled decision actually lives: the
  six ruled-out options in the roadmap, the constraints encoded in interface signatures, the
  operational defaults argued in the options classes.

## [Standards](standards/index.md)

- [How standards are enforced](standards/how-standards-are-enforced.md) — the six conventions
  that are checked by a tool rather than described in prose, what each fails on, and the two
  anti-patterns to refuse when tempted to write a style guide.

## [Runbooks](runbooks/index.md)

- [Running Loop.Engine](runbooks/running-loop-engine.md) — secrets and which key each model id
  needs; pointing it at a repository; a first run that cannot write anything; the four stage
  flags in dependency order; where artifacts land; a symptom table for a tick that did nothing;
  two known sharp edges.

---

**Status** — every file carries one in frontmatter. `current`: intended behaviour today, still
verify against code. `living`: changes under you. `historical`: explains *why*, never *what is*.

**Not here.** Skills (`.claude/skills/`) are their own progressive-disclosure layer — each
`SKILL.md` loads its own leaves on demand. Invoke the skill; do not index it.

**Adding a concept.** Add the file *and* a line above it. A concept nobody can route to is a
concept nobody loads. Describe what is inside the document, not its subject — "permission
validation, idempotency rules" beats "information about the tool executor". A domain earns its
own `index.md` only when it passes roughly seven concepts; below that this page is the whole map.
