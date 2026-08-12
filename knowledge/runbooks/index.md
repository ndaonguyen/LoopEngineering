---
type: Index
title: "Runbooks"
description: "Local map of the runbooks domain - operating procedures a person drives by hand, and where the procedures that live beside their design can be found."
status: current
---

# Runbooks

What to type, in what order, and what to check when it does nothing.

**Belongs here** — a procedure not derivable from the code: credentials supplied out of band,
flags that must be enabled in a particular order, artifacts that land somewhere non-obvious, or
symptoms whose cause is several layers from where they surface. **Does not** — why the system is
shaped that way ([../architecture/](../architecture/index.md)).

## Concepts

- [running-loop-engine.md](running-loop-engine.md) — secrets and which key each model id needs;
  pointing the engine at a repository; a first run that cannot write anything; the four stage
  flags in dependency order; where each artifact lands; a symptom table for a tick that produced
  nothing; two known sharp edges.

## Procedures kept elsewhere

Some procedures sit beside the design they operate, because the guardrails and the invocation are
read together:

- **Running the skills bug loop** — [../architecture/bug-loop.md § Running it](../architecture/bug-loop.md#running-it)
- **Build and test commands** — [../../README.md](../../README.md)

---

Full tree: [../index.md](../index.md)
