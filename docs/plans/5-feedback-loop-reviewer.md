# Plan: #5 — Phase 4: Feedback loop + Reviewer Agent

Issue: <https://github.com/ndaonguyen/LoopEngineering/issues/5>
Input: [docs/inputs/5-feedback-loop-reviewer.md](../inputs/5-feedback-loop-reviewer.md)
Roadmap: [docs/bug-loop-roadmap.md](../bug-loop-roadmap.md) → Phase 4

## Branch & PR

- **Branch:** `feat/5-feedback-loop`
- **PR title:** `feat: add the build-test feedback loop and Reviewer agent (#5)`

---

## Context

Every phase so far *reasons*. This one *checks*. A compiler and a test runner are the first
components in the pipeline that can falsify a claim, and the session that built Phases 2–3
produced two concrete reminders of why that matters: the investigation confidently repeated
a false premise from an issue body, and the Coder's first run truncated its own output
without anything noticing until a human read the log.

It also closes Phase 3's open exit criterion. #4 asked for "a diff that compiles"; only the
*parses* half is automated. This phase's build step is that check done properly — and its
first real exercise is the existing `fix-8.diff`.

## Decisions

| Decision | Choice | Why |
| --- | --- | --- |
| Where the build runs | **`git worktree` under a temp directory** | `EditWorkspace` holds only allow-listed files — not a buildable tree. A worktree is a full checkout that costs no re-clone, and Phase 5 needs one anyway. Verified available on this repo. |
| Failure signal | **Compiler errors + test failures. Warnings never.** | This repo already carries `NU1902`/`NU1903` advisories on `OpenTelemetry.*` and `Microsoft.OpenApi` that predate every agent. Treating warnings as failure loops five times on day one, against something no fix can address. |
| Test scope | Configurable project filter, defaulting to `tests/Loop.Engine.Tests` | The full suite is ~60s because `Api.Tests` spins up a host; ×5 attempts is five minutes of loop for tests unrelated to any fix the agent makes. |
| Build/test invocation | Behind an **`IBuildRunner` port** | Otherwise the test suite needs a real compiler and a real clock. The fake returns canned results; one integration test exercises the real runner. |
| Reviewer timing | **After the build and tests go green** | Reviewing a broken diff spends a call describing problems the compiler already named. Its findings are advisory output, not retry input. |
| Retry shape | Each attempt must **state what it now believes was wrong** before proposing a change | Appending the error and re-asking produces variations on the same wrong idea — the failure mode the `bug-loop` skill already documents. Force a changed hypothesis, not a changed line. |

---

## Changes, in order

### 1. `Loop.Engine.Core` — ports and results

**Files:** `Abstractions/IBuildRunner.cs`, `Abstractions/IReviewer.cs`, `Model/BuildResult.cs`, `Model/ReviewReport.cs`

```csharp
Task<BuildResult> BuildAsync(string workingDirectory, CancellationToken ct);
Task<BuildResult> TestAsync(string workingDirectory, string? filter, CancellationToken ct);
```

- `BuildResult(bool Succeeded, IReadOnlyList<string> Errors, string RawOutput)`.
  **Errors only** — warnings are not carried, so they cannot accidentally become a signal.
- `ReviewReport(IReadOnlyList<ReviewFinding> Findings)`; `ReviewFinding(string Category,
  string Severity, string Detail)`.

**Pattern to mimic:** `Abstractions/ICoder.cs` — the port exposes what the phase may do and
nothing more.

### 2. `Loop.Engine.Agents/Verification` — the runner

**Files:** `DotnetBuildRunner.cs`, `BuildOutputParser.cs`

- `DotnetBuildRunner` — shells `dotnet build` / `dotnet test`, captures stdout and stderr
  **separately** (the Phase 3 lesson: git's CRLF advisories nearly landed inside a diff).
- `BuildOutputParser` — pure, extracts `error CSxxxx` lines and xunit `[FAIL]` entries.
  Explicitly **ignores** `warning` lines. Pure so it is assertable against recorded output.

### 3. `Loop.Engine.Agents/Verification` — the worktree

**Files:** `FixWorktree.cs`

- `git worktree add --detach <temp> HEAD`, apply the Coder's `CodeEdit` list by **writing
  file contents** (not `git apply` — the diff's paths are workspace-relative and were never
  meant to be applied).
- `IDisposable`: `git worktree remove --force`, then delete the directory.
- Never touches the user's checkout or current branch.

### 4. `Loop.Engine.Agents/Verification` — the loop

**Files:** `FixVerifier.cs`, `RepairPrompt.cs`, `VerificationOptions.cs`

- Attempt 1 is the Coder's original output. On failure: build a repair prompt carrying the
  **compiler errors and test failures verbatim**, plus every hypothesis already tried.
- The repair prompt requires the model to state *what it now believes was wrong* before the
  new code. A retry that cannot articulate a changed diagnosis is repeating itself.
- `MaxAttempts` default **5**, hard. On exhaustion throw with a stuck report: what was
  tried, what the last failure was, what remains unknown.

### 5. `Loop.Engine.Agents/Review` — the Reviewer

**Files:** `ReviewerAgent.cs`, `ReviewerPrompt.cs`, `ReviewDto.cs`

- Reads the unified diff; returns findings across security, performance, naming,
  architecture, clean code.
- **No workspace, no file writes, no `ICoder`.** Same structural enforcement as the Coder's
  allow-list: a critic that can rewrite stops being a critic.

### 6. Wire into the tick

**Files:** `IssuePollingService.cs`, `PipelineOptions.cs`, `DependencyInjection.cs`, `appsettings.json`

- `Pipeline:VerifyFix` (default **false**) gates the whole phase, as `GenerateFix` gates
  Phase 3. Each phase stays off until asked for; the previous phase remains the baseline.
- On success write `fix-<n>.diff` (verified) and `review-<n>.md` beside the report.

### 7. Tests

**Files:** `FixVerifierTests.cs`, `BuildOutputParserTests.cs`, `ReviewerAgentTests.cs`, `Fakes/FakeBuildRunner.cs`

- `FakeBuildRunner` — canned results in sequence, so the loop is testable with no compiler.
- **Exit-criterion test:** a fake that fails twice then succeeds must recover, in ≤3 calls.
- Attempts are capped: a runner that always fails throws after exactly 5, not 6.
- `BuildOutputParser`: real recorded `dotnet build` output in, errors out, **warnings
  ignored** — including this repo's actual `NU1902` line as a fixture.
- `ReviewerAgent`: has no path to a writable surface; findings parse from prompt-carried JSON.

---

## Acceptance

| Issue requirement | Verified by |
| --- | --- |
| Run `dotnet build` after code generation | `DotnetBuildRunner` + integration test |
| Run `dotnet test` | Same |
| Collect compiler errors, warnings, test failures | `BuildOutputParserTests` — errors and failures collected, **warnings deliberately not** |
| Feed them back to the model | `RepairPrompt` carries them verbatim; asserted |
| Retry, capped at 5 | `FixVerifierTests` — exactly 5, then a stuck report |
| Reviewer never edits code | No writable dependency exists on `IReviewer` |
| **Exit criterion:** broken build recovers within 5 attempts | `FixVerifierTests`; then **manually** against `fix-8.diff` |

## Risks

- **The loop is slow.** ~15s build + ~45s full test suite × 5 attempts. Hence the default
  test filter. Do not "temporarily" raise the cap to compensate.
- **Warnings creeping into the signal.** The repo's existing `NU1902`/`NU1903` advisories
  would loop forever. The parser ignores warnings by construction, and a test pins it.
- **Retries that repeat themselves.** Mitigated by requiring a stated hypothesis, but worth
  watching in the first real run: if attempts 2–5 differ only cosmetically, the prompt is
  not doing its job.
- **Worktree cleanup on a crash.** `git worktree remove --force` in `Dispose`, but a hard
  kill leaves one behind. `git worktree prune` in the runner's startup path.

## Out of scope

Branches, commits, PRs (Phase 5) · fixing #8 by hand · auto-merging anything ·
treating warnings as failures.

## Migration

None — no entity changes.
