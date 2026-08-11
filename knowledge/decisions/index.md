---
type: Index
title: "Decisions"
description: "Local map of the decisions domain - settled choices and their reasoning, and the deliberate absence of ADR files."
status: current
---

# Decisions

Choices that are settled: the trade-offs accepted, the alternatives ruled out, and what would
have to change to reopen them.

**Belongs here** — a decision whose reasoning is *not* already recorded next to the code it
governs. **Does not** — anything argued in an XML doc three lines from the signature it
constrains. That version is read by the person about to violate it; a file here is not.

## Concepts

- [how-decisions-are-recorded.md](how-decisions-are-recorded.md) — why there are no ADR files,
  the three-part bar for writing one, and where every settled decision actually lives: the six
  ruled-out options in the roadmap, the constraints encoded in interface signatures, the
  operational defaults argued in the options classes.

## Why this folder is nearly empty

Not an oversight. This repo records decisions closer to the code than a separate ADR would put
them, and duplicating them here would create a second copy that drifts from the first. The
document above names the promotion candidates and the bar they must clear.

---

Full tree: [../index.md](../index.md)
