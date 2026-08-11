---
type: Planning Brief
title: "Input #5 - Phase 4: Feedback loop + Reviewer Agent"
description: "The brief that produced plan #5 - constraints, non-goals and ruled-out approaches, as they stood at the time."
status: historical
---

# Input: #5 — Phase 4: Feedback loop + Reviewer Agent

## Issue

<https://github.com/ndaonguyen/LoopEngineering/issues/5>

Build and test the Coder's output, feed the failures back, and try again — capped. Plus a
Reviewer that reads the diff and comments without editing.

This is the phase that separates a demo from a tool, and it is the first stage in the whole
pipeline that can **falsify** anything. Phases 2 and 3 reason; a compiler and a test runner
decide. Everything upstream has been a claim, and this is where claims meet evidence.

> **It also closes Phase 3's open exit criterion.** #4 required "a diff that compiles" and
> only the *parses* half was automated — `SyntaxVerifier` is deliberately named for what it
> does. Phase 4's build step is that check, done properly. First real exercise: run it
> against the existing `fix-8.diff`.

## Design / Reference Links

- [docs/bug-loop-roadmap.md](../bug-loop-roadmap.md) — **Phase 4** is authoritative.
- [docs/bug-loop-proposal.md](../bug-loop-proposal.md) — sections *6. Testing Agent* and
  *7. Reviewer Agent*.
- [docs/plans/4-planner-coder.md](../plans/4-planner-coder.md) — the verified-claims table,
  particularly **parsing ≠ compiling**, which is what this phase exists to fix.
- `source/Loop.Engine.Agents/Coding/` — `EditWorkspace`, `SyntaxVerifier`, `DiffGenerator`,
  `CoderAgent`. The retry loop wraps the Coder; it does not replace it.
- `source/Loop.Engine.Agents/Coding/CoderAgent.cs` — the per-file call pattern, adopted
  after a single whole-set call truncated on the first real run.

## Brainstorming

**Decided — carried from Phases 2 and 3, all proven in code:**

- `IChatClient`, prompt-carried JSON contract, `TolerantJson`. Do not revisit.
- `FakeChatClient` for every test. No network, no key.
- Per-file model calls where output is large — established because a single call blew the
  token budget on a real run.
- Fail loudly with `FinishReason` / `TextLength` / `MaxOutputTokens` in the message. Those
  three numbers have diagnosed every model failure this project has had.

**Open for the planner:**

- **Where the build runs.** `EditWorkspace` currently holds only the allow-listed files —
  not a buildable tree. Options: copy the whole repo into the workspace, use a
  `git worktree`, or apply edits to a scratch clone. Phase 5 needs a worktree anyway.
- **What counts as a failure signal.** Compiler errors clearly. Warnings? Probably not —
  this repo already has pre-existing `NU1902`/`NU1903` vulnerability warnings that have
  nothing to do with any fix, and treating them as failure would loop forever on day one.
- **Whether the Reviewer runs before or after the tests pass.** Reviewing a broken diff
  wastes a call; reviewing only a green one means its findings arrive too late to shape
  the retry.
- **How a retry differs from the first attempt.** Appending the error and re-asking tends
  to produce variations on the same wrong idea. Consider requiring the retry prompt to
  state what it now believes was wrong.

**Ruled out, do not re-propose:**

- Treating warnings as build failures. See above.
- Letting the Reviewer edit code. The issue is explicit, and a critic that can rewrite
  stops being a critic.
- Removing the attempt cap "just for this run".

## Constraints & Non-goals

- **Maximum 5 attempts, then stop and explain.** A hard limit, not a suggestion. The
  explanation must say what was tried and what the last failure was — a precise stuck
  report is a successful outcome; an infinite loop is not.
- **The Reviewer never edits.** It reads the diff and reports: security, performance,
  naming, architecture, clean code.
- **No PR, branch, or commit.** Phase 5.
- **No writes to the user's working tree.** Everything in a discardable workspace, as in
  Phase 3.
- **Do not break Phases 1–3.** 56 tests currently green; keep them that way.
- **Every test runs offline** — build/test invocation must be behind a seam a fake can
  satisfy, or the suite starts requiring a real compiler and a real network.
- **`Confidence` still gates nothing**, including the decision to retry.
- **Exit criterion:** a deliberately broken build recovers on its own within 5 attempts,
  and the Reviewer produces a report on the resulting diff.
