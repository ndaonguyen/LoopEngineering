---
type: Runbook
title: "Running Loop.Engine"
description: "Operating procedure for the engine - secrets setup, turning the four stage flags on in dependency order, a safe first run that cannot open a pull request, where the artifacts land, and what to check when a tick does nothing."
status: current
---

# Running Loop.Engine

The operating procedure. What the engine *is* and why it is shaped this way is in
[loop-engine.md](../architecture/loop-engine.md); this is what you type.

**Read this before the first run on a real repository.** All four stage flags ship **off**, so
a fresh clone investigates and stops. Turning them on in the wrong order is the main way to be
surprised, and the last one opens a pull request.

---

## 1. Secrets

Two credentials. Neither goes in a committed file.

```bash
dotnet user-secrets set "GitHub:Token" (gh auth token) --project source/Loop.Engine
```

The token needs **write** scope. A read-only token lists issues but silently fails to label
them, which looks like the pipeline doing nothing.

The model key can be a user-secret or an environment variable — the engine checks both:

```bash
dotnet user-secrets set "Ai:AnthropicApiKey" sk-ant-api03-... --project source/Loop.Engine
```

| Model id starts with | Provider | Key setting | Environment variable |
|---|---|---|---|
| `claude-` | Anthropic | `Ai:AnthropicApiKey` | `ANTHROPIC_API_KEY` |
| `gpt-`, `o1`, `o3`, `o4`, `chatgpt` | OpenAI | `Ai:OpenAiApiKey` | `OPENAI_API_KEY` |

Anything else fails at startup naming the accepted prefixes. Startup also checks that the key
*shape* matches its provider (`sk-ant-` vs `sk-`), so a key in the wrong slot is caught before a
network round trip rather than surfacing as an opaque 401 inside a poll failure.

`Ai:Model` is the workhorse (Coder, Reviewer, repair loop). `Ai:ReasoningModel` serves
Investigation and Planning, where being wrong is most expensive — every later stage inherits the
file list and the plan. Leave it empty to use one model everywhere; the two may name different
providers.

## 2. Point it at a repository

```jsonc
"Repository": { "RootPath": "C:/MINE/LoopEngineering1" }
```

**Use an absolute path.** A relative one resolves against two different anchors depending on
which component reads it — see [Sharp edges](#sharp-edges) — and an absolute path is treated
identically by both.

`GitHub:Owner` and `GitHub:Repository` must name the same repository, because the fix is built
from `origin/<Publishing:BaseBranch>` and the pull request is opened against it.

## 3. First run: investigate one issue, change nothing

Pin the issue and stop after one tick. With every stage flag off, this cannot write to your
repository or to GitHub.

```bash
dotnet run --project source/Loop.Engine -- --Pipeline:RunOnce=true --Pipeline:IssueNumber=8
```

Expect: the open-issue report, then `Investigated #8 -> …/investigation-8.md`, an affected-file
count, and the run cost. **Read the investigation and check the files it named are the right
ones.** Every later stage inherits that list; if retrieval is wrong, nothing downstream recovers.

The issue must carry the `Pipeline:RequiredLabel` label (default `bug`). Naming an issue
explicitly does **not** override the label check — being asked for a specific issue says which
one, not that it is a bug.

## 4. Turn the stages on, one at a time

Each flag requires the one above it. Add one, run, read the artifact, then add the next.

| Add this flag | You get | It still cannot |
|---|---|---|
| `--Pipeline:GenerateFix=true` | A plan and a `fix-<n>.diff` | build, branch, or push |
| `--Pipeline:ReproduceFirst=true` | A failing test written **before** the fix, run against unfixed code. Only a red result continues | proceed on a test that passes or does not compile |
| `--Pipeline:VerifyFix=true` | Format → build → test → repair (up to `Verification:MaxAttempts`, default 5) → review | publish anything |
| `--Pipeline:PublishPr=true` | Branch, commit, push, **open a pull request** | merge, approve, or close it |

Run `ReproduceFirst` before trusting `VerifyFix`. Without it a green build only proves the
change broke nothing — not that it fixed anything.

Once you are happy, set the flags in `appsettings.json` instead of passing them each time.

## 5. What lands where

Artifacts go to `Ai:OutputDirectory`, resolved against the **working directory** — which for
`dotnet run --project source/Loop.Engine` is `source/Loop.Engine/`, so the default lands in
`source/Loop.Engine/output/investigations/`.

| File | Written when | Notes |
|---|---|---|
| `investigation-<n>.md` | always | The file list to sanity-check |
| `reproduction-<n>.md` | `ReproduceFirst` | Written **whether or not the test passed the gate** — a rejected test you cannot read is not a diagnosis |
| `fix-<n>.diff` | `GenerateFix` | Survives a failed publish |
| `review-<n>.md` | `VerifyFix` | Repair history, run cost, findings |

The build happens in a **throwaway git worktree** under the system temp directory
(`loop-engine-wt-<guid>`), created from `origin/<BaseBranch>` — not from `HEAD`. Your checkout
is never switched or dirtied. It is removed on exit; a hard kill leaves it behind, and the next
run prunes it.

## 6. When a tick does nothing

Every refusal is logged as a sentence. Match it here.

| What you see | Cause | Fix |
|---|---|---|
| `Configuration error in …` then exit code 1 | A required setting is missing or malformed | The listed failures name the setting |
| `No open issue is labelled 'bug'` | Nothing eligible | Label an issue, or change `Pipeline:RequiredLabel` |
| `Pipeline:IssueNumber=N … is not among the N open issue(s)` | Closed, or wrong repository | Check `GitHub:Owner` / `GitHub:Repository` |
| `… is not labelled 'bug'` | The label check, working as intended | Label it, or change the required label |
| `Reproduction rejected (NotProduced)` | The model returned nothing usable | Read the plan; the investigation may not have given it enough to write against |
| `Reproduction rejected (DoesNotCompile)` | The generated test does not build | Read `reproduction-<n>.md`; usually it was written against signatures that do not exist |
| `Reproduction rejected (AlreadyPasses)` | The test passes against **unfixed** code, so it proves nothing. The most dangerous outcome — it would pass after the fix too | Same file: the assertion is wrong, or the bug is not where the investigation thought |
| `Reproduction rejected (TestNotFound)` | The filter matched no test — a mistyped fully-qualified name | Check the name in `reproduction-<n>.md`. This is distinguished on purpose: `dotnet test` exits non-zero for it exactly as for a real failure |
| `Coder produced no textual change` | The model returned nothing applicable | Check the plan in the log; often the investigation named the wrong files |
| `Publish failed: …` | Push or PR creation failed | The verified diff and review are still on disk; the branch can be pushed by hand |
| Nothing at all, no error | Poll threw and was swallowed to protect the scheduler | Look for `Poll failed; retrying on the next tick` above |

A tick that produced no model calls prints no cost block. If you see a cost block and no
artifact, a stage failed after spending money — that is the run worth reading closely.

## 7. After a pull request opens

The engine stops. It cannot merge, approve, or close — those methods do not exist on either
publisher interface.

- A PR labelled **`needs-human`** means the automated review raised a **high**-severity finding.
  It is a signal, not a gate: the PR opens either way, and the warning is also in the body.
  If the label does not exist in the repository the labelling step logs a warning and continues.
- The branch prefix is `fix/`, matching the skills loop, so both are legible in one branch list.
- PR titles are `fix:` — [pr-lint.yml](../../.github/workflows/pr-lint.yml) enforces Conventional
  Commits.

## Sharp edges

**`Repository:RootPath` has two resolution rules.**
[FileRetriever](../../source/Loop.Engine.Agents/Retrieval/FileRetriever.cs) anchors a relative path
to the *application* directory (`AppContext.BaseDirectory`) — the fix for issue #8, so the same
configuration cannot point at different trees depending on where the engine was launched.
[FixWorktree.Create](../../source/Loop.Engine.Agents/Verification/FixWorktree.cs) calls
`Path.GetFullPath`, which anchors to the *process working* directory. The shipped default
`"../.."` therefore means the repository root to the worktree and `source/Loop.Engine/bin` to
the retriever. In practice retrieval never sees it — the pipeline repoints it at the worktree by
absolute path before any stage runs — but the two rules disagree, and **an absolute path is read
identically by both**. Worth reconciling.

**`Verification:TestProject` defaults to the engine's own test project**, not the solution. A fix
to `LoopEngineering.*` is verified against `Loop.Engine.Tests`, which will not exercise it. Set
it per target repository.

**Costs are only reported if you supply the rates.** `Ai:InputCostPerMillion` /
`OutputCostPerMillion` default to `0`, which prints tokens and no money. There is no built-in
price table on purpose — a stale rate is wrong in a way nobody notices.
