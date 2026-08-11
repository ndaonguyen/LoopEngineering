---
type: Implementation Plan
title: "Plan #4 - Phase 3: Planner + Coding Agent"
description: "The section-by-section implementation plan for issue #4. Shipped - useful for the reasoning behind a decision, useless as a description of the code."
status: historical
---

# Plan: #4 — Phase 3: Planner + Coding Agent

Issue: <https://github.com/ndaonguyen/LoopEngineering/issues/4>
Input: [docs/inputs/4-planner-coder.md](../inputs/4-planner-coder.md)
Roadmap: [docs/bug-loop-roadmap.md](../bug-loop-roadmap.md) → Phase 3

## Branch & PR

- **Branch:** `feat/4-planner-coder`
- **PR title:** `feat: add the Planner and Coding agents (#4)`

---

## Context

Phase 2 shipped and passed its exit criterion: given [#8](https://github.com/ndaonguyen/LoopEngineering/issues/8)
the Investigation agent named the three files the defect actually lives in. This phase
consumes that output.

`FixPlan` / `FixPlanStep` **already exist** in `Loop.Engine.Core/Model` — written in
Phase 1 with `FixPlanStep.Files` as the Coder's allow-list. This is what they were for.

The split matters for a specific reason: a model asked to plan and code in one breath
starts editing before deciding what it is doing, and the "plan" becomes a narration of
edits already made. Two agents, two prompts, two artifacts, and the plan is fixed before
any file is touched.

## Decisions

| Decision | Choice | Why |
| --- | --- | --- |
| SDK & structured output | `IChatClient` + **`ReportParser`-style prompt-carried JSON contract** | Phase 2 established this the hard way: `GetResponseAsync<T>` does not produce JSON through this provider, and neither did `useJsonSchemaResponseFormat: true`. The contract lives in the prompt; parsing tolerates fences and wrappers. Do not re-litigate. |
| Edit staging | **Copy allow-listed files to a temp workspace**, never touch the working tree | A bad run must be discardable. Phase 5 needs a worktree; this is deliberately smaller — a worktree here would drag in git plumbing this phase does not need. |
| Diff generation | **`git diff --no-index`** against the staged copies | The CLI is already a hard dependency of the project's purpose. LibGit2Sharp is a package for something `git` does natively. |
| Roslyn | **Syntax verification only** — parse each edited file, reject on error diagnostics | Text editing suffices to *produce* a diff. Roslyn catches a malformed edit before it reaches Phase 4, cheaply. Full AST rewriting only if the text Coder demonstrably fails. **See the parse-vs-compile limit below — this does not satisfy the issue's "compiles" wording on its own.** |
| Allow-list enforcement | **In the type signature**, not the prompt | The Coder receives `IReadOnlyList<RetrievedFile>` and has no repository access at all. A constraint the model is merely asked to honour lapses; one it cannot physically violate does not. |

### Verified by compiling, not assumed

| Claim | Result |
| --- | --- |
| `git diff --no-index` exit 1 = differences, 0 = identical | ✅ confirmed |
| `CSharpSyntaxTree.ParseText` flags a missing brace | ✅ 2 errors, `CS1513: } expected` |
| `CSharpSyntaxTree.ParseText` flags non-C# garbage | ✅ 2 errors |
| `CSharpSyntaxTree.ParseText` flags a call to an undefined method | ❌ **0 errors** |

**Parsing is not compiling.** `class A { void M() { Undefined(); } }` parses cleanly — a
Coder edit that calls a method that does not exist, misspells an identifier, or passes the
wrong type sails past `SyntaxVerifier` and fails `dotnet build`. Semantic diagnostics need
a `CSharpCompilation` with the full reference set, which is most of a build, which is
Phase 4.

So the issue's exit criterion — *"produces a git diff that compiles"* — **cannot be fully
met inside this phase**. Split it honestly:

- `SyntaxVerifier` catches malformed edits automatically. Cheap, real, and it stops the
  most common failure mode from reaching Phase 4 at all.
- "Compiles" is confirmed by a **human running `dotnet build` on the applied diff** for #8,
  once. Phase 4 then automates it, which is exactly what Phase 4 is for.

Do not close the gap by growing a compilation step here. That is Phase 4.

### Line endings will corrupt the diff if ignored

The `git diff --no-index` probe emitted `LF will be replaced by CRLF` warnings on this
machine. If the Coder returns files with `\n` while the originals on disk are `\r\n`,
**every line shows as changed** and the diff is worthless.

`EditWorkspace` must therefore detect each original file's dominant line ending and write
the replacement using the same one. This is the third time line endings have bitten this
project — the Phase 1 test failure and the CI-vs-Windows mismatch were the first two — so
treat it as a known hazard, not an edge case. Redirect the command's stderr separately so
those warnings never end up inside the captured diff text.

---

## Changes, in order

### 1. `Loop.Engine.Core` — the ports

**Files:** `Abstractions/IPlanner.cs`, `Abstractions/ICoder.cs`, `Model/CodeEdit.cs` (all new)

```csharp
Task<FixPlan> PlanAsync(Issue issue, AnalysisResult analysis, CancellationToken ct);
Task<CodeChangeSet> WriteCodeAsync(Issue issue, AnalysisResult analysis, FixPlan plan, CancellationToken ct);
```

- `CodeEdit(string RelativePath, string NewContents)` and
  `CodeChangeSet(IReadOnlyList<CodeEdit> Edits, string UnifiedDiff)`.
- Neither port exposes a repository path or a search capability. That is the enforcement.

**Pattern to mimic:** `Abstractions/IInvestigator.cs` — port in Core, no vendor types, no
capability the phase should not have.

### 2. `Loop.Engine.Agents/Planning` — the Planner

**Files:** `PlannerAgent.cs`, `PlannerPrompt.cs`, `PlanDto.cs`, `PlanParser.cs`

- Consumes `AnalysisResult`; emits `FixPlan`.
- Every `FixPlanStep.Files` entry **must** be a subset of `AnalysisResult.AffectedFiles` —
  filter and log discards, exactly as `InvestigationAgent.KeepOnlyRetrieved` does. A step
  naming a file outside the investigation is the plan leaking scope.
- A plan with zero steps is an error, not an empty success. Throw.

**Pattern to mimic:** `Investigation/InvestigationPrompt.cs` + `ReportParser.cs`. The
prompt states the JSON shape and field names explicitly — Phase 2 proved that omitting
them yields prose.

### 3. `Loop.Engine.Agents/Coding` — the workspace

**Files:** `Coding/EditWorkspace.cs`, `Coding/WorkspaceOptions.cs`

- Copies only the allow-listed files into a temp directory, preserving relative paths.
- **Records each file's dominant line ending on copy and re-applies it on write.** Without
  this the diff shows every line as changed on Windows.
- `IDisposable` — deletes the workspace on dispose, so a failed run leaves nothing behind.
- Refuses any write whose resolved path escapes the workspace root. The Coder proposes
  paths; a path is untrusted input.

### 4. `Loop.Engine.Agents/Coding` — the Coder

**Files:** `CoderAgent.cs`, `CoderPrompt.cs`, `CodeEditDto.cs`, `EditParser.cs`

- Receives issue + analysis + plan + the allow-listed file contents. **No retriever, no
  repo root, no glob.**
- Returns whole-file replacements rather than patches: a model producing a valid unified
  diff by hand is markedly less reliable than one producing a correct file, and `git`
  computes the diff for us.
- Discard any edit whose path is not in the allow-list, and log it.

### 5. `Loop.Engine.Agents/Coding` — verification and diff

**Files:** `Coding/SyntaxVerifier.cs`, `Coding/DiffGenerator.cs`

- `SyntaxVerifier` — `CSharpSyntaxTree.ParseText` per edited `.cs` file; reject on
  diagnostics of severity Error. Roslyn's whole role here. It catches syntax, **not**
  semantics: an undefined method call parses clean (verified). Name the type
  `SyntaxVerifier`, not `CompileVerifier`, so nobody later mistakes it for a build.
- `DiffGenerator` — runs `git diff --no-index --` original vs edited, returns the unified
  diff. **Exit code 1 means "differences found" and is success**; only ≥2 is failure.
  Capture stdout and stderr separately — git's CRLF warnings must not land in the diff.

**Package:** `Microsoft.CodeAnalysis.CSharp` on `Loop.Engine.Agents`.

### 6. Wire into the tick

**Files:** `Loop.Engine.Worker/Pipeline/IssuePollingService.cs`, `Loop.Engine.Agents/DependencyInjection.cs`, `Loop.Engine/appsettings.json`

- After investigating, plan, then code, then write `fix-<n>.diff` beside the report.
- Gate behind `Pipeline:GenerateFix` (default **false**) so Phase 2 behaviour is unchanged
  unless asked for. Phase 2 is the working baseline; do not regress it by default.

### 7. Tests

**Files:** `tests/Loop.Engine.Tests/` — `PlannerAgentTests.cs`, `CoderAgentTests.cs`,
`EditWorkspaceTests.cs`, `SyntaxVerifierTests.cs`, `PlanParserTests.cs`

- `FakeChatClient` throughout. **No network, no API key** — unchanged hard requirement.
- Planner: drops steps citing files outside `AffectedFiles`; throws on an empty plan.
- Coder: drops edits outside the allow-list; a returned path of `../../etc/passwd` is
  rejected by the workspace, not merely filtered.
- `SyntaxVerifier`: valid C# passes; a missing brace fails.
- Reuse the `Normalize()` line-ending helper — CI is Linux, dev is Windows.

---

## Acceptance

| Issue requirement | Verified by |
| --- | --- |
| Planner consumes the investigation, emits ordered tasks | `PlannerAgentTests` |
| Coder modifies files / creates files / updates tests | `CoderAgentTests` |
| Generates a git diff | `DiffGenerator` + manual run |
| Does **not** open a PR | No git remote call exists in this phase |
| Coder cannot explore the repo | Enforced by `ICoder`'s signature — no path, no retriever |
| **Exit criterion:** the diff compiles | Split — `SyntaxVerifier` proves it *parses* (automated); **a human runs `dotnet build` on the applied diff for #8** to prove it *compiles*. Parsing ≠ compiling; see the verified-claims table. |

## Risks

- **The Coder inherits a wrong premise.** Phase 2's report on #8 repeated a false claim
  from the issue text. Nothing here can falsify it — Phase 4 is the first stage that can.
  Do not add a half-verification loop to compensate; that is Phase 4's entire job and
  building it twice means building it badly twice.
- **Whole-file replacement scales badly.** Fine for the ~400-line files this repo has;
  revisit if a target file is large.
- **`git diff --no-index` exit codes.** 1 means differences, not failure. Getting this
  backwards produces a silent empty diff.
- **Line endings.** A `\n` replacement over a `\r\n` original marks every line changed.
  Third occurrence in this project; handled in `EditWorkspace`, but worth watching for.
- **Mistaking parse for compile.** `SyntaxVerifier` returning clean does not mean the diff
  builds — undefined methods and type errors pass it. Do not report the exit criterion met
  on the strength of a green verifier alone.

## Out of scope

Branches, commits, PRs (Phase 5) · running build or tests (Phase 4) · AST *rewriting* ·
embeddings · fixing #8 by hand.

## Migration

None — no entity changes.
