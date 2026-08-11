---
type: Index
title: "Execution artifacts"
description: "The map of docs/. Every record of what was done and why it was done that way, grouped by kind, with enough description to pick the right one without opening it."
status: current
---

# Execution artifacts

What was **done**, and why it was done that way. How the system is *designed* lives in
[../knowledge/index.md](../knowledge/index.md).

Reach in here for the history behind a decision — not to learn how anything behaves today.

## Loop.Engine delivery

- [bug-loop-roadmap.md](bug-loop-roadmap.md) — each phase's purpose and exit criterion, what the
  first real runs exposed when measured against issue #8, the sprint table, the technology
  choices, and the six things **deliberately not being done** with the reasoning for each.
  `living`.
- [bug-loop-proposal.md](bug-loop-proposal.md) — an externally-sourced multi-agent design
  (scheduler → investigate → plan → code → build → PR), recorded for comparison against what
  actually shipped. Explicitly **not adopted**; describes nothing that runs. `historical`.

## Bug-loop memory

- [bug-loop-learnings.md](bug-loop-learnings.md) — cross-issue debugging knowledge: test seams
  that were non-obvious, hypotheses that were plausible and wrong, error messages that point away
  from their cause. Read at `fix-bug-issue` Step 4, appended at Step 8 so entries ride in a fix PR
  and get human review. Not a changelog — no entry is the normal outcome. `living`.

## Per-issue paperwork

- [plans/](plans/) — section-by-section implementation plans for merged work: branch name and PR
  title, ordered changes with the sibling file each should mimic, and an acceptance-criteria
  traceability table. Useful for the reasoning behind a decision, useless as a description of the
  code. `historical`.
- [inputs/](inputs/) — the briefs that produced those plans: issue restatement, design and
  reference links, brainstorming, constraints and non-goals as they stood at the time.
  `historical`.
- [inputs/TEMPLATE.md](inputs/TEMPLATE.md) — the live format `/plan-issue` expects. Copy it to
  start a new brief. `current`.

---

**Status** — every file carries one in frontmatter. Everything here is `historical` except the
roadmap and the debugging memory, which are `living`.

**Numbering.** The file numbers in `plans/` and `inputs/` are **issue** numbers, and the phase
numbering is offset by one: issue #3 delivered Phase 2, #6 delivered Phase 5.

**Why the memory lives here.** It is written by execution — the loop appends to it after each
fix — so it sits with the artifacts rather than in `knowledge/`, even though it is read as input
to the next diagnosis.
