---
type: Convention
title: "How decisions are recorded"
description: "Why this repo has no ADR files, the three-part bar for writing one, and where each settled decision actually lives - the ruled-out options in the roadmap, the constraints encoded in interface signatures, the operational defaults in the options classes."
status: current
---

# How decisions are recorded

Choices that are settled, why they were made, and what would have to change to reopen them.

**No ADR files exist yet.** That is deliberate — this repo's decisions are already written down
with their reasoning, closer to the code than a separate ADR would put them. Writing ADRs now
would create a second copy that drifts from the first. This index routes to where each decision
actually lives, and records the bar for promoting one into a file here.

## When a decision earns a file in this folder

Write an ADR only when **all three** hold:

1. The decision constrains future work — someone could plausibly undo it by accident.
2. Its reasoning is **not** already recorded next to the code it governs.
3. The alternatives were real, and the reason for rejecting them is not obvious from the outcome.

A decision whose reasoning already sits in an XML doc comment three lines from the code it
governs fails test 2. Move it here only if it outgrows that home.

## Where the decisions currently live

### Ruled out, with reasoning — [docs/bug-loop-roadmap.md § Deliberately not doing](../../docs/bug-loop-roadmap.md#deliberately-not-doing)

Six decisions, each recorded because it looks like an obvious improvement and each would undo
something that was paid for:

| Decision | Why it stands |
|---|---|
| No concurrent issues | One bug at a time is a decision, not a limitation. Parallel branches racing make the first failures harder to read, and reading failures is the bottleneck. |
| `MaxAttempts` stays at 5 | Lifting the cap trades a bounded failure for an unbounded one. |
| The loop never merges | Structural, not policy: neither publisher interface has a merge, approve, or close method. A model cannot call what does not exist. |
| The two loops stay separate | The skills loop is the daily driver; `Loop.Engine` is the deployable product. Different jobs. |
| The model does not choose which files to read | `SymbolExtractor` and `FileRetriever` score candidates with fixed weights so retrieval stays testable and reproducible. |
| One source of truth about the working tree | Every stage shares one worktree. Two trees disagreed twice in this project's history; once the repair loop deleted working code the compiler could not see and reported success throughout. |

**These are the strongest candidates for promotion into this folder** — they are already ADRs in
everything but filename and location. Promoting them means *moving* the section, not rewriting it.

### Encoded in the type system — `Loop.Engine.Core/Abstractions`

The most binding decisions in this codebase are expressed as **absent** interface methods, each
with its reasoning in the XML doc above it: `ICoder` has no repository path, `IReviewer` has no
workspace, `IGitPublisher` has no merge or force-push. Summarised in
[Loop.Engine architecture](../architecture/loop-engine.md#the-constraint-that-lives-in-the-signatures).

Do not lift these into ADRs. The reasoning belongs beside the signature it explains — that is
what stops someone adding a convenience method without reading it.

### Delivery trade-offs — [docs/bug-loop-roadmap.md](../../docs/bug-loop-roadmap.md)

Each shipped phase records its exit criterion and, where the implementation deviated from the
plan, why. Phase 9 is the clearest example: the plan said report formatting failures, the
implementation fixes them instead, and the paragraph explains the change. `living`.

### Operational defaults — the options classes

`AiOptions` ships no built-in price table (a stale rate is wrong in a way nobody notices);
`ModelProviders` infers the provider from the model id rather than taking a second setting (two
ways to say one thing is one new way to be wrong). Reasoning is in the XML docs.
