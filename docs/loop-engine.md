---
type: Service Architecture
title: Loop.Engine
description: The autonomous bug-fixing engineer — port/adapter boundary, the stage gates from issue to pull request, and the constraints each stage is structurally unable to violate.
status: current
---

# Loop.Engine

A .NET worker that polls GitHub issues and drives one bug through **investigate → plan →
reproduce → code → verify → review → publish**. It stops at an open pull request. **A human
merges** — and no interface in the system can express a merge.

This document covers boundaries, invariants, and the reasoning that is not visible in a
signature. It does not list classes; the code does that better. For *why each phase was
built*, see [bug-loop-roadmap.md](bug-loop-roadmap.md). For the skills-based loop that does the
same job in prompts rather than C#, see [bug-loop.md](bug-loop.md).

---

## The boundary

```
Loop.Engine.Core        ports + model. No project references. Depends on nothing.
   ↑            ↑
Agents        GitHub    adapters. Both implement Core's ports. Never reference each other.
   ↑            ↑
      Worker            the only composer: Pipeline/IssuePollingService wires the stages.
        ↑
   Loop.Engine          host. Configuration, DI, the OptionsValidationException handler.
```

**`Loop.Engine.Core` has no project references and must keep it none.** It holds the ports
(`Abstractions/`) and the data passed between them (`Model/`). Every other project depends on
it; it depends on nothing, so a port can never acquire a dependency on the thing implementing
it.

**`Agents` and `GitHub` must never reference each other.** They are siblings, both adapters.
Their only shared vocabulary is `Core`. An agent that could reach GitHub directly would let the
model's output become an API call with nothing in between.

**`Worker/Pipeline` is the only place stages are composed.** If two components need to know
about each other's order, that knowledge belongs in `IssuePollingService`, not in either one.

[tests/Architecture.Tests](../tests/Architecture.Tests/ArchitectureTests.cs) enforces all three
rules by parsing the `.csproj` files. It holds no project references of its own — a test project
that referenced everything in order to inspect everything would be the one edge the graph could
never see. Adding a project under `source/` fails that suite until it is placed in the
allow-list deliberately.

## The constraint that lives in the signatures

The most important design decision in this codebase is not written in prose. Several ports are
defined by **what they cannot express**:

| Port | Cannot | Because |
|---|---|---|
| `ICoder` | No repository path, no retriever, no search | It can only touch files the investigation identified, because those are the only files it is given. A constraint a model is *asked* to honour lapses under pressure; one it cannot physically violate does not. |
| `IReproducer` | Same, plus no way to run anything | Whether the test goes red is decided by the compiler and the runner, never by the model that wrote it. |
| `IReviewer` | No workspace, no file access, no coder | A critic that can rewrite what it is criticising stops being a critic. |
| `IInvestigator` | No way to change anything | Keeps the Planner tractable; a port that could write code would erode the split by the first deadline. |
| `IGitPublisher` | No merge, no force-push, no delete | "Never merges" is the whole product. Leaving it to a prompt would make it a preference. |
| `IPullRequestPublisher` | Cannot merge, approve, or close | Same reason. |

**Do not add convenience methods to these interfaces.** Every absent method above is load-bearing.
The guarantee is structural: a model cannot call what does not exist.

`IBuildRunner` and `IFixWorkspace` exist for a different reason — they keep the test suite
offline. Depending on a real compiler or a real git repository would make every test of the
retry loop cost a real minute, and the offline-tests rule would quietly become "offline except
for builds".

## Stage gates

Each phase is **off by default** and gated by a `Pipeline` flag. Adding a phase must not change
what the engine does unless it is asked for; the previous phase stays the baseline.

```
poll → select → investigate → [GenerateFix] plan → [ReproduceFirst] red gate
     → code → [VerifyFix] format → build → test → repair → review → [PublishPr] branch → PR
```

| Flag | Requires | What it turns on |
|---|---|---|
| `GenerateFix` | — | Planner + Coder run; a `.diff` is written. No branch, no build. |
| `ReproduceFirst` | `GenerateFix` | Write a failing test *before* the fix and refuse to continue unless it goes red. |
| `VerifyFix` | `GenerateFix` | Format, build, test, repair on failure, then review. |
| `PublishPr` | `VerifyFix` | Push the verified worktree to a branch and open the PR. |

Two of these orderings are not arrangements of convenience:

- **The red gate runs before the fix exists.** That is the only moment a test can prove
  anything. A test written against already-fixed code cannot demonstrate that it ever caught
  the bug. Only `ReproductionOutcome.Red` is permission to proceed — `NotProduced` and
  `DoesNotCompile` stop the run.
- **Formatting runs before the build**, so what compiles, what the tests run against, and what
  gets pushed are the same bytes. Formatting afterwards would publish something the pipeline
  never verified.

Verification is the first stage that can **falsify** anything. Everything upstream reasons
about the code; here a compiler and a test runner decide.

## Invariants

- **Bugs only.** Only issues carrying `Pipeline:RequiredLabel` (default `bug`) are eligible, and
  an explicit `Pipeline:IssueNumber` **does not override the label** — being asked for a
  specific issue says which one, not that it is a bug.
- **One issue per tick, oldest first.** Concurrent branches racing each other only make the
  first failures harder to read.
- **One worktree per run, shared by every stage** — retrieval, coding, building, publishing.
  Two trees disagreed twice in this project's history; once the repair loop resolved the
  disagreement by deleting code the compiler could not see, and reported success throughout.
- **`MaxAttempts` is a hard limit (default 5).** Raising it to get past a stubborn bug trades a
  bounded failure for an unbounded one. The honest outcome after five tries is a stuck report.
- **A failed publish never discards a verified fix** — the diff and review are already on disk.
- **A transient failure never kills the scheduler.** Log it, wait for the next tick.
- **Retrieval is rule-based, not model-chosen.** `SymbolExtractor` and `FileRetriever` score
  candidates with fixed weights so retrieval stays testable and reproducible.
- **Every test runs offline.** No test may need a network, a real compiler, or a real
  repository. This is why the ports above exist.

## Configuration

All sections bind at startup with `ValidateOnStart`, so a missing setting is an operator error
reported in one readable line — not forty frames of DI internals.

| Section | Key settings | Notes |
|---|---|---|
| `GitHub` | `Owner`, `Repository`, `Token`, `PollInterval` | Token needs **write** scope; a read-only token lists issues but silently fails to label them, which looks like the pipeline doing nothing. Supply via user-secrets or `GitHub__Token`. **Never commit it.** |
| `Ai` | `Model`, `ReasoningModel`, `MaxOutputTokens`, `OutputDirectory`, cost-per-million | Two chat clients: the default serves Coder/Reviewer/repair, the keyed `reasoning` client serves Investigation and Planning, where a wrong answer poisons everything downstream. Both are wrapped in `MeteredChatClient`, so a new agent is measured the moment it is written. Startup validates that model ids resolve to a provider, that each provider has a key, and that the key *shape* matches its provider. |
| `Pipeline` | `RequiredLabel`, `IssueNumber`, `RunOnce`, the four stage flags | See above. |
| `Publishing` | `BranchPrefix` (`fix/`), `Remote`, `BaseBranch`, `NeedsHumanLabel` | `fix/` matches the skills loop so both are legible in one branch list. `needs-human` is a signal, not a gate — the PR opens either way. |
| `Verification` | `MaxAttempts`, `TestProject`, `Timeout`, `FormatGeneratedCode` | `TestProject` defaults to the engine's own tests: the full solution takes ~60s because the API tests spin up a host, and five attempts of that is five minutes on tests unrelated to the fix. `FormatGeneratedCode` is **on** by default — unlike the stage flags — because it changes nothing outward-facing, only whether the whitespace is right. |
| `Repository` | `RootPath`, `IncludeGlobs`, `MaxFiles`, `MaxLinesPerFile` | `RootPath` is repointed at the run's worktree for the duration of the run, so the files the model is shown are the files that will be compiled and published. |

## Where to look when changing something

| Changing… | Start at |
|---|---|
| What a stage is allowed to do | `Loop.Engine.Core/Abstractions` — the XML docs there carry the reasoning |
| The order stages run in | `Loop.Engine.Worker/Pipeline/IssuePollingService.cs` |
| Which issue a tick picks | `Loop.Engine.Worker/Pipeline/IssueSelector.cs` — pure, assert it against plain lists |
| What the model is shown | `Loop.Engine.Agents/Retrieval/` |
| Whether a fix is accepted | `Loop.Engine.Agents/Verification/FixVerifier.cs`, `Reproduction/ReproductionGate.cs` |
| Branch, commit, PR body | `Loop.Engine.GitHub/Publishing/` |

## Deliberately not doing

Recorded because each looks like an obvious improvement and each would undo something paid for.
The full list with reasoning is in
[bug-loop-roadmap.md](bug-loop-roadmap.md#deliberately-not-doing): concurrent issues, raising
`MaxAttempts`, letting the loop merge, merging the two loops, letting the model choose which
files to read, and a second source of truth about the working tree.
