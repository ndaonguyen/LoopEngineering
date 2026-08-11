---
type: Index
title: "Runbooks"
description: "Map of the runbooks domain - operating procedures for the things in this repo that a person has to drive by hand."
status: current
---

# Runbooks

Operating procedures. What to type, in what order, and what to check when it does nothing.

A runbook belongs here when the procedure is **not** derivable from the code — credentials that
must be supplied out of band, flags that must be enabled in a particular order, artifacts that
land somewhere non-obvious, or symptoms whose cause is several layers from where they surface.

## Procedures

| Runbook | Read it when you need | Status |
|---|---|---|
| [Running Loop.Engine](running-loop-engine.md) | Setting up the GitHub token and model key and knowing which key each model id needs; pointing the engine at a repository; a first run that cannot write anything; turning the four stage flags on in dependency order; where each artifact lands; a symptom table for a tick that produced nothing; the two known sharp edges. | current |

## Elsewhere

- **Running the skills bug loop** — in [../architecture/bug-loop.md § Running it](../architecture/bug-loop.md#running-it),
  kept beside the design because the loop's guardrails and its invocation are read together.
- **Local development, migrations, deploy** — in
  [../architecture/database.md](../architecture/database.md).
- **Build and test commands** — [../../README.md](../../README.md).
