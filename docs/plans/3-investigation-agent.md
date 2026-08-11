---
type: Implementation Plan
title: "Plan #3 - Phase 2: Investigation Agent"
description: "The section-by-section implementation plan for issue #3. Shipped - useful for the reasoning behind a decision, useless as a description of the code."
status: historical
---

# Plan: #3 — Phase 2: Investigation Agent

Issue: <https://github.com/ndaonguyen/LoopEngineering/issues/3>
Input: [docs/inputs/3-investigation-agent.md](../inputs/3-investigation-agent.md)
Roadmap: [docs/bug-loop-roadmap.md](../bug-loop-roadmap.md) → Phase 2

## Branch & PR

- **Branch:** `feat/3-investigation-agent`
- **PR title:** `feat: add the Investigation Agent (#3)`

---

## Context

Phase 1 (#2, merged in `64d8f59`) left `Loop.Engine.Agents` empty and `AnalysisResult`
unused. This phase fills both: given an issue number, produce an `investigation.md` that
names the files actually involved, and stop there.

The value of the phase is the *separation*, not the report. Phase 3's Planner and Coder
only work if something upstream has already decided which files are in play — so the
allow-list this produces (`AnalysisResult.AffectedFiles`) is the real deliverable, and the
markdown is the human-readable view of it.

## Decisions taken before planning

| Decision | Choice | Why |
| --- | --- | --- |
| AI SDK | **`Anthropic` NuGet package** (official, v12.39.0) via its `Microsoft.Extensions.AI` `IChatClient` integration | One package, not a community shim. `IChatClient` is trivially fakeable, so the agent is testable with no network and no API key. Semantic Kernel's kernels/plugins/planners buy orchestration Phase 2 doesn't need. |
| Model | `claude-opus-5` | The SDK's documented default. Diagnosis is the reasoning-heavy step; this is not where to economise. |
| Retrieval | Keyword + path heuristics | Stack-trace bugs carry their own retrieval keys. No index to keep fresh. Embeddings deferred to Sprint 2 — see the input file's ruled-out list. |
| Report shape | **Structured output via `IChatClient.GetResponseAsync<T>()`** → then rendered to markdown | Resolves an open question from the input file. Parsing prose for a file list is fragile; a schema makes `AffectedFiles` a typed contract Phase 3 can depend on. Markdown becomes a pure render of the parsed object. |
| Repo checkout | **Point at a local path; do not clone** | Resolves the second open question. The engine already runs beside the repo, and cloning adds credentials and disk management this phase doesn't need. `RepositoryOptions.RootPath`, defaulted to the solution root. Cloning becomes a concern when Phase 5 needs a worktree. |
| Effort / thinking | Start at **`effort: high`**, adaptive thinking on | Diagnosis is the reasoning-heavy step. `IChatClient` hides these knobs — reach them through `ChatOptions.RawRepresentationFactory`. Do not silently take the default; make it a config value so it can be swept later. |

### API shapes — verified by compiling against `Anthropic` 12.39.0

These were checked, not assumed. Use them verbatim:

```csharp
IChatClient chat = new AnthropicClient().AsIChatClient("claude-opus-5");

// Structured output — this is the Extensions.AI layer, NOT Anthropic's OutputConfig.Format,
// which is unreachable through IChatClient by design.
ChatResponse<InvestigationReport> typed = await chat.GetResponseAsync<InvestigationReport>(prompt);
InvestigationReport? report = typed.Result;

// Provider-specific knobs (thinking, effort) escape hatch:
var options = new ChatOptions { RawRepresentationFactory = _ => /* Anthropic params */ };
```

`Anthropic` 12.39.0 depends on `Microsoft.Extensions.AI.Abstractions` 10.5.1 and ships
`net8.0` / `net9.0` / `netstandard2.0` assets — no `net10.0` folder. It resolves via the
`net9.0` assets on this solution's `net10.0` target and builds clean. Not a problem; just
don't go looking for a `net10.0` lib directory.

---

## Changes, in order

### 1. `Loop.Engine.Core` — extend the contract

**Files:** `Abstractions/IInvestigator.cs` (new), `Model/AnalysisResult.cs` (edit)

- Add `IInvestigator` with `Task<AnalysisResult> InvestigateAsync(Issue, CancellationToken)`.
- `AnalysisResult` already has the right shape (`Symptoms`, `PossibleRootCauses`,
  `AffectedFiles`, `Confidence`, `Markdown`). Leave it alone unless the schema forces a
  change — it was written for this phase.

**Pattern to mimic:** `Abstractions/IIssueSource.cs` — port in Core, adapter elsewhere,
no vendor types in the signature.

### 2. `Loop.Engine.Agents` — retrieval

**Files:** `Retrieval/SymbolExtractor.cs`, `Retrieval/FileRetriever.cs`, `Retrieval/RepositoryOptions.cs`

- `SymbolExtractor` — **pure static class**, no I/O. Pull candidate identifiers from an
  issue's title + body: stack-trace frames (`Namespace.Type.Method`), `*.cs` filenames,
  and PascalCase tokens. Returns them ranked, stack-trace symbols first.
- `FileRetriever` — glob the repo for files whose name or contents match those symbols.
  Cap at ~15 files and ~2000 lines total; the model gets an excerpt, not the repo.
- `RepositoryOptions` — `RootPath`, `IncludeGlobs` (default `source/**/*.cs`,
  `tests/**/*.cs`), `MaxFiles`.

**Pattern to mimic:** `Worker/Pipeline/IssueReportFormatter.cs` — the pure/impure split
that made Phase 1 testable. `SymbolExtractor` is the pure half here and gets the same
treatment.

### 3. `Loop.Engine.Agents` — the agent

**Files:** `Investigation/InvestigationAgent.cs`, `Investigation/InvestigationPrompt.cs`, `Investigation/InvestigationReport.cs`

- `InvestigationAgent : IInvestigator`, constructor-injected `IChatClient` (from
  `Microsoft.Extensions.AI`), `FileRetriever`, `IOptions<InvestigationOptions>`, logger.
- `InvestigationReport` — the DTO `GetResponseAsync<InvestigationReport>()` fills in:
  `symptoms`, `possible_root_causes[]`, `affected_files[]`, `confidence`,
  `recommended_investigation`. Mapped onto `AnalysisResult` after the call. A `null`
  `ChatResponse<T>.Result` means the model returned something unparseable — **throw, don't
  return an empty `AnalysisResult`**, or Phase 3 receives an empty allow-list and silently
  investigates nothing.
- `InvestigationPrompt` — **static, pure, and therefore assertable in a test.** Builds the
  system + user message from the issue and the retrieved excerpts.
- The prompt must state the constraint the issue states: *investigate, do not propose code
  changes.* Ask for the mechanism and the observation seam, not a fix.

### 4. `Loop.Engine.Agents` — rendering + DI

**Files:** `Investigation/InvestigationMarkdown.cs`, `DependencyInjection.cs`

- `InvestigationMarkdown.Render(AnalysisResult, Issue)` → the `investigation.md` body.
  Pure function; asserted directly.
- `AddLoopEngineAgents(IConfiguration)` — bind `InvestigationOptions` and
  `RepositoryOptions` with `ValidateDataAnnotations().ValidateOnStart()`, register
  `IChatClient` via `new AnthropicClient().AsIChatClient(options.Model)`, register
  `IInvestigator`.
- **Credentials:** the SDK reads `ANTHROPIC_API_KEY` from the environment by default.
  Support that *and* an explicit `Anthropic:ApiKey` config value, and make the
  `ValidateOnStart()` check assert on whichever one is actually in force — otherwise the
  fail-loud check passes while the credential the client uses is missing.

**Pattern to mimic:** `Loop.Engine.GitHub/DependencyInjection.cs` — same options binding,
same `ValidateOnStart()`. That fail-loud-at-startup choice is why a missing GitHub token
doesn't masquerade as "no issues found"; a missing API key must not masquerade as "no
findings".

### 5. `Loop.Engine` — wire it into the tick

**Files:** `Program.cs`, `appsettings.json`, `Loop.Engine.Worker/Pipeline/IssuePollingService.cs`

- Register the agents module.
- `appsettings.json`: `Anthropic:Model` = `claude-opus-5`, `Anthropic:ApiKey` = `""`
  (empty, exactly as `GitHub:Token` ships), `Repository:RootPath`.
- The polling service investigates the **first** open issue per tick and writes
  `investigation.md` to the output directory. One issue per tick — Phase 2 is not the
  place to introduce concurrency.

### 6. Tests

**Files:** `tests/Loop.Engine.Tests/Loop.Engine.Tests.csproj` (edit), `SymbolExtractorTests.cs`, `InvestigationPromptTests.cs`, `InvestigationMarkdownTests.cs`, `InvestigationAgentTests.cs`, `Fakes/FakeChatClient.cs`

- **Add a `ProjectReference` to `Loop.Engine.Agents`** — the test project currently
  references only `Core` and `Worker`, so none of the below compiles without it.
- `FakeChatClient : IChatClient` — returns a canned JSON payload. **No network, no API
  key, in any test.** This is a hard requirement from the input file, not a preference.
- `SymbolExtractorTests` — a real stack trace in, the right type and method names out.
- `InvestigationPromptTests` — the prompt contains the issue body, the retrieved excerpts,
  and the no-code-generation instruction.
- `InvestigationAgentTests` — a fake response maps onto `AnalysisResult` with
  `AffectedFiles` populated; a malformed response fails loudly rather than returning an
  empty result.

**Pattern to mimic:** `tests/Loop.Engine.Tests/IssueReportFormatterTests.cs`, including its
`Normalize()` line-ending helper — **CI runs `ubuntu-latest` and your dev box is Windows**,
and that mismatch already broke one test in Phase 1.

---

## Acceptance

| Issue requirement | Verified by |
| --- | --- |
| Download / index / retrieve relevant files | `SymbolExtractorTests`, `FileRetrieverTests` |
| LLM investigation step | `InvestigationAgentTests` with `FakeChatClient` |
| Emits `investigation.md` | `InvestigationMarkdownTests` + a manual run against a real issue |
| Report has symptoms · causes · classes · confidence · recommendation | `InvestigationReport` schema + markdown test |
| **No code generation** | No `Write`/`Edit` path exists in `Loop.Engine.Agents`; enforced by design, not intention |
| Exit criterion | **Manual:** run against a real bug issue and read the report. The files must actually be right. A green test suite does not satisfy this. |

## Risks

- **The exit criterion is human judgement.** A report can be well-formed, confident, and
  wrong. Budget for reading a few and tuning the prompt; do not treat first-green as done.
- **`Confidence` is recorded and must gate nothing.** It is an uncalibrated self-report.
  If Phase 3 starts branching on it, that's a bug.
- **Retrieval fails silently on issues without stack traces.** Acceptable this phase —
  the loop's own `fix-bug-issue` skill already refuses issues with no repro. If the
  retriever finds nothing, say so in the report rather than sending an empty context.

## Scope changes against the original issue

The plan narrows #3 in three places. **Issue #3 has been amended to match** — these are
recorded here so the divergence is auditable rather than buried in a decisions table.

| Original checklist item | Now | Why |
| --- | --- | --- |
| "Download / check out the target repository" | Point at a local path | The engine runs beside the repo. Cloning adds credential and disk management with no payoff until Phase 5 needs a worktree. |
| "Index source files" | Glob on demand | An index is a second source of truth that goes stale. At this repo's size a glob is faster than keeping one fresh. |
| Learn "Roslyn syntax-tree basics" | Deferred to Phase 3 | Retrieval needs to find *files*, which text matching does. Roslyn earns its place when the Coder needs to modify syntax, not read it. |

## Out of scope

Embeddings/pgvector · cloning repositories · any file mutation · PR or branch activity ·
Roslyn (Phase 3) · the Planner (Phase 3).

## Migration

None — no entity changes, so no EF migration.
