---
type: Planning Brief
title: "Input #3 - Phase 2: Investigation Agent"
description: "The brief that produced plan #3 - constraints, non-goals and ruled-out approaches, as they stood at the time."
status: historical
---

# Input: #3 — Phase 2: Investigation Agent

## Issue

<https://github.com/ndaonguyen/LoopEngineering/issues/3>

Give `Loop.Engine` its first AI stage. Handed an issue number, it checks out the target
repository, finds the files plausibly involved, asks a model to **investigate** the defect,
and writes an `investigation.md`. The reframing from *"fix this bug"* to *"investigate this
bug"* is the point of the phase — separating analysis from coding is what makes Phase 3's
Planner and Coder tractable.

Builds directly on Phase 1 (#2, merged): `IIssueSource`, the `Issue`/`IssueTask` model, and
`AnalysisResult`, which already exists as the output shape for this stage.

## Design / Reference Links

- [docs/bug-loop-roadmap.md](../bug-loop-roadmap.md) — **Phase 2** section is authoritative
  for scope and exit criterion.
- [docs/bug-loop-proposal.md](../bug-loop-proposal.md) — section *3. Investigation Agent*
  for the report's required fields.
- [CLAUDE.md](../../CLAUDE.md) — repo conventions (layer layout under `source/`, xunit +
  AwesomeAssertions, Conventional Commits).
- `source/Loop.Engine.Core/Model/AnalysisResult.cs` — the existing output contract.
- `source/Loop.Engine.Agents/` — empty since Phase 1; this is where the agent lands.

## Brainstorming

**Decided:**

- **Microsoft.Extensions.AI (`IChatClient`) with Claude** as the provider. Chosen over
  Semantic Kernel because SK's kernels/plugins/planners buy orchestration that isn't needed
  until Phase 3–4, and `IChatClient` is trivially fakeable — the agent gets tested without a
  network call or an API key.
- **Keyword and path heuristics for retrieval**, not embeddings. Extract type names, method
  names, and file paths from the stack trace and issue body; glob the repo for them. No
  vector DB, no index to keep fresh. Stack-trace bugs are the ones worth automating first,
  and they carry their own retrieval keys.

**Ruled out, do not re-propose:**

- Embeddings + pgvector this phase — deferred to Sprint 2. Postgres already being in
  `docker-compose.yml` is not a reason to reach for it now.
- Sending the whole repository to the model — cost scales with repo size, and it removes
  the retrieval step that Phase 3 depends on for its file allow-list.

**Open for the planner:**

- Where the repo checkout lives (temp dir vs. a persistent workspace) and whether Phase 2
  clones at all, given the engine may already be running inside the repo.
- Whether `AnalysisResult.Markdown` is rendered by the agent or by a separate formatter —
  Phase 1 split `IssueReportFormatter` out as a pure function for exactly this reason.

## Constraints & Non-goals

- **No code generation.** The agent must not modify, create, or propose source edits. If
  this phase writes a line of production code, the separation Phase 3 relies on is already
  gone. Enforce it in the design, not just by intention.
- **No PR, branch, or commit activity.** That is Phase 5.
- **The API key is never committed.** `dotnet user-secrets` locally, environment variable in
  CI. `appsettings.json` ships the key empty, matching how `GitHub:Token` is handled today.
- **Do not break Phase 1's contract.** `IIssueSource`, `Issue`, `IssueTask` and the existing
  `IssuePollingService` behaviour stay working; `dotnet test` stays green.
- **The agent must be testable without a network call** — a fake `IChatClient` is a hard
  requirement, not a nice-to-have.
- Confidence is recorded on `AnalysisResult` but **must not gate anything**. It is an
  uncalibrated self-report; treating it as a decision input is the failure this repo has
  already flagged twice.
- **Exit criterion** (from the roadmap): given an issue number, the system emits an
  `investigation.md` naming the files actually involved — verified by a human reading it,
  not by the model asserting it.
