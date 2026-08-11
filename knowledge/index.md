---
type: Index
title: "Knowledge layer"
description: "Root map of the OKF knowledge tree - how the system is designed. Routes to the architecture, decisions, standards and runbook domains; execution artifacts live in docs/ and are not indexed here."
status: current
---

# Knowledge layer

How the system is **designed**. This tree answers "what must stay true", not "what did we do".

| Tree | Holds | Example |
|---|---|---|
| `knowledge/` | Concepts: boundaries, invariants, contracts, decisions, operating procedures | "The Coder may not read the repository" |
| `docs/` | Execution artifacts: plans, briefs, phase records, superseded proposals | "Plan #4 — Phase 3: Planner + Coding Agent" |

If a document describes what someone *did*, it belongs in `docs/`. If it describes what must
*remain true*, it belongs here.

## How to use this tree

1. Identify the affected domain from the task, then open **that domain's `index.md`** — not this
   file's whole subtree.
2. Load only the concepts the domain index names.
3. Compare each concept against the code before relying on it. A concept states intent; the code
   states behaviour.
4. When they conflict, report both. Never silently pick a side.
5. Skip anything marked `historical` unless you need to know *why* a decision was made.

**Status** — how far a document can be trusted on its own:

| | |
|---|---|
| `current` | Describes intended behaviour today. Still verify against code. |
| `living` | Changes as work happens; expect it to move under you. |
| `historical` | Explains *why*, not *what is*. Never cite as current behaviour. |

## Domains

| Domain | Covers |
|---|---|
| [architecture/](architecture/index.md) | Project boundaries and what may reference what, the two bug-fixing systems and how they differ, the auth design and its persistence, the constraints encoded as absent interface methods. |
| [decisions/](decisions/index.md) | Choices that are settled and their reasoning — the trade-offs accepted, the alternatives ruled out, and what would have to change to reopen them. |
| [standards/](standards/index.md) | Conventions a tool cannot check. Everything a tool *can* check is enforced, not documented — this index routes to the enforcement. |
| [runbooks/](runbooks/index.md) | Operating procedures a person drives by hand — credentials supplied out of band, flags that must be enabled in order, where artifacts land, and what a silent failure means. |

## Not in this tree

**Skills** (`.claude/skills/`) are their own progressive-disclosure layer. Each `SKILL.md` is an
entry point that loads its own leaf documents on demand — `tdd/SKILL.md` pulls in `tests.md`,
`mocking.md`, `deep-modules.md` and `interface-design.md` only when it needs them. Indexing them
here would flatten that and load a workflow's internals before anyone asked for the workflow.
**Invoke the skill; do not index it.**

**Execution artifacts** (`docs/`) — plans, briefs, phase records, superseded proposals. They
explain what was done and why it was done that way. Reach for them when you need the history
behind a decision, not when you need to know how the system behaves today.

## Adding a concept

Put it in the domain whose index you would have looked in. If two domains both seem right, the
concept is probably two concepts.

A new file needs the four frontmatter fields (`type`, `title`, `description`, `status`) and a row
in its domain index. **The row is not optional** — a concept nobody can route to is a concept
nobody loads. Write the description as selection signal: name the things inside the document, not
its subject. "Tool execution boundaries, permission validation, idempotency rules" beats
"information about the tool executor".

A domain earns its own subtree when it passes roughly seven concepts. Below that, a table in the
domain index is the whole map.
