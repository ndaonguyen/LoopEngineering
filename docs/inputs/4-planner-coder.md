# Input: #4 — Phase 3: Planner + Coding Agent

## Issue

<https://github.com/ndaonguyen/LoopEngineering/issues/4>

Split thinking from coding. The **Planner** turns Phase 2's `AnalysisResult` into an
ordered list of discrete tasks; the **Coder** takes the issue, the report, the plan, and
*only the files the investigation identified*, and produces a git diff. No PR — that is
Phase 5.

The point of the split is that a model asked to plan and code in one breath does neither
well: it starts editing before it has decided what it is doing, and the plan becomes a
post-hoc narration of the edits. Two stages, two prompts, two artifacts.

> ⚠️ **Blocked on Phase 2's exit criterion.** #3 is implemented and green, but nobody has
> yet read a real `investigation.md` and confirmed the files it names are the right ones.
> The Coder is restricted to that file list — so if retrieval is weak, this phase inherits
> the problem and the failure surfaces as "the Coder edited the wrong thing", which is a
> much more expensive place to discover it. Do not plan this issue until #3 has been run
> for real against a live key.

## Design / Reference Links

- [docs/bug-loop-roadmap.md](../bug-loop-roadmap.md) — **Phase 3** is authoritative for
  scope and exit criterion.
- [docs/bug-loop-proposal.md](../bug-loop-proposal.md) — sections *4. Planner Agent* and
  *5. Coding Agent*.
- [docs/plans/3-investigation-agent.md](../plans/3-investigation-agent.md) — the shape to
  mimic, including the verified API block.
- `source/Loop.Engine.Core/Model/FixPlan.cs` — **already exists from Phase 1.**
  `FixPlanStep.Files` was written to be the Coder's allow-list; this phase is what it was
  for. Do not redesign it without a reason.
- `source/Loop.Engine.Agents/Investigation/` — the working reference implementation: pure
  prompt class, structured-output DTO, agent, hallucination filter.

## Brainstorming

**Decided — carry forward from Phase 2, all proven in code:**

- Same stack: `Anthropic` + `Microsoft.Extensions.AI` `IChatClient`, structured output via
  `GetResponseAsync<T>()`. Do not reach for `OutputConfig.Format` — unreachable through
  the abstraction.
- Same testing seam: `FakeChatClient`. Every test offline, no key.
- Same failure posture: `TryGetResult`, throw with a message naming the issue. Do not
  return an empty plan — an empty plan reads as "nothing to do" and the pipeline reports
  success having done nothing.
- Same anti-hallucination filter: the Coder must not touch a path outside the plan's
  allow-list, exactly as `InvestigationAgent.KeepOnlyRetrieved` drops invented paths.

**Roslyn — narrower than the issue implies.** #4 lists "Roslyn · AST · function
extraction". Text editing is sufficient to *produce* a compiling diff, so Roslyn's real
value here is **verification**: parse each edited file and reject the edit if it no longer
parses, before anything is written. That is cheap, high-signal, and satisfies the issue's
Roslyn item without building a syntax-tree rewriting engine. Propose a full AST-rewrite
approach only if the plain-text Coder demonstrably fails.

**Ruled out, do not re-propose:**

- Giving the Coder repo-wide search or glob access. The issue says so, and it is the whole
  reason Phase 2 exists. An agent with the run of the repo will find something to change
  whether or not it is the bug.
- Writing directly into the user's working tree. Edits belong in a staging copy so a bad
  run is discarded rather than cleaned up by hand.
- Opening branches, commits, or PRs. Phase 5.

**Open for the planner:**

- Where edits land — temp directory copy, `git worktree`, or in-memory patch set. Note
  Phase 5 needs a worktree anyway; doing it here may save the work twice.
- Whether the Coder sees all allow-listed files at once or one task at a time. One-at-a-
  time is more focused and more round trips.
- How the diff is produced — `git diff` CLI, LibGit2Sharp, or hand-rolled unified diff.
  LibGit2Sharp is already on the roadmap's tech list but adds a dependency for something
  the CLI does natively.

## Constraints & Non-goals

- **The Coder receives only the plan's allow-listed files.** Enforce it in the type
  signature, not in the prompt — a constraint the model is merely asked to respect is a
  constraint that lapses under pressure.
- **No PR, branch, or commit activity.** Phase 5.
- **No writes to the user's working tree.** Stage edits somewhere discardable.
- **Do not break Phases 1–2.** `IIssueSource`, `IInvestigator`, `IssuePollingService` keep
  working; `dotnet test` stays green (currently 50 tests).
- **Every test runs offline.** `FakeChatClient`, no network, no API key — same hard
  requirement as Phase 2.
- **`Confidence` still gates nothing**, including the decision to proceed to coding.
- **Exit criterion:** the pipeline produces a git diff that **compiles**. Correctness is
  explicitly Phase 4's job — do not smuggle a verification loop into this phase, because
  the feedback loop is what Phase 4 *is*.
