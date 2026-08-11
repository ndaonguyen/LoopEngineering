---
type: Implementation Plan
title: "Plan #6 - Phase 5: GitHub automation, issue to open PR"
description: "The section-by-section implementation plan for issue #6. Shipped - useful for the reasoning behind a decision, useless as a description of the code."
status: historical
---

# Plan: #6 — Phase 5: GitHub automation, issue to open PR

Issue: <https://github.com/ndaonguyen/LoopEngineering/issues/6>
Input: [docs/inputs/6-github-automation.md](../inputs/6-github-automation.md)
Roadmap: [docs/bug-loop-roadmap.md](../bug-loop-roadmap.md) → Phase 5

## Branch & PR

- **Branch:** `feat/6-github-automation`
- **PR title:** `feat: publish verified fixes as pull requests (#6)`

---

## Context

Phases 1–4 are working: the last run on [#8](https://github.com/ndaonguyen/LoopEngineering/issues/8)
went investigate → plan → code → build → test → review, green on the first verification
attempt. This phase publishes that result and stops.

Most of the pieces already exist. `PullRequestContext` was written in Phase 1 with
`RenderBody()` and `Closes #<n>`; `FixWorktree` already creates, applies to, and cleans up
a worktree; `GitHubOptions` and the Octokit client are wired. This is assembly, not
invention — which is worth saying plainly, because the temptation in a final phase is to
build something impressive rather than something small.

## Decisions

| Decision | Choice | Why |
| --- | --- | --- |
| Where the branch is created | **The same worktree Phase 4 verified in** | The verified tree is exactly the content that should be published. Branching from anywhere else risks publishing something subtly different from what passed the build — which is precisely how the earlier working-tree/HEAD mismatch destroyed code. |
| Gating | **Only when `VerificationResult.Succeeded`** | A PR containing a fix nobody built is worse than no PR: it carries the appearance of review. |
| Branch name | `fix/<issue>-<slug>`, matching the `bug-loop` skill | One convention, so both loops are legible in the same branch list. Collisions are handled below rather than avoided by inventing a second scheme. |
| Existing branch | **Refuse, do not force-push** | A second run silently overwriting the first destroys a PR a human may already be reading. Fail with the branch name and let a person decide. |
| Git invocation | Behind an **`IGitPublisher`** port; Octokit behind **`IPullRequestPublisher`** | Otherwise the suite needs a real remote. Same seam that kept the compiler out of the tests in Phase 4. |
| Commit identity | The worktree's inherited git config | Already set (`ndaonguyen`). Configuring an identity per run adds a setting whose only job is to disagree with git's. |

### Verified by running it, not assumed

| Claim | Result |
| --- | --- |
| `git push` to a local **bare** repo works with no network | ✅ — makes `GitPublisherTests` real git, offline |
| `git ls-remote --exit-code --heads origin <branch>` | ✅ exit **0** when present, **2** when absent |
| Octokit `client.PullRequest.Create(owner, repo, new NewPullRequest(title, head, baseRef) { Body })` → `.Number`, `.HtmlUrl` | ✅ compiles |
| `Octokit` is available in `Loop.Engine.Agents` | ❌ **not referenced there** |

Two things fall out of that table.

**`ls-remote` returns 2, not 1, when the branch is absent.** Treating "non-zero means
error" would report every new branch as a failure. Check `exit == 0` to mean *exists* —
the same exit-code trap as `git diff --no-index`, where 1 means "differences found" and is
success. Twice now in this project.

**The PR publisher belongs in `Loop.Engine.GitHub`, not `Loop.Engine.Agents`.** Octokit and
`GitHubOptions` already live there, and that project is the GitHub adapter by design.
Adding Octokit to `Agents` would give the agent layer a second way to reach the network.
`GitPublisher` (shelling `git`) can stay in `Agents` — it touches the filesystem, not the
API — but putting both in `Loop.Engine.GitHub` keeps "things that talk to GitHub" in one
project. **Prefer that.**

---

## Changes, in order

### 1. `Loop.Engine.Core` — ports

**Files:** `Abstractions/IGitPublisher.cs`, `Abstractions/IPullRequestPublisher.cs`, `Model/PublishedPullRequest.cs`

```csharp
Task<string> PublishBranchAsync(string worktreePath, string branch, string message, CancellationToken ct);
Task<PublishedPullRequest> OpenAsync(PullRequestContext context, CancellationToken ct);
```

- `PublishedPullRequest(int Number, string Url)`.
- Neither port exposes merge, approve, or close. **The invariant is the absent method**, as
  with `ICoder` having no repository path and `IReviewer` having no writable dependency.

### 2. `Loop.Engine.GitHub/Publishing` — git

**Files:** `GitPublisher.cs`, `PublishingOptions.cs`

- `git checkout -b <branch>` → `git add -A` → `git commit` → `git push -u origin <branch>`,
  run in the verified worktree, stdout and stderr captured **separately**.
- **Refuse if the branch already exists** locally or on the remote
  (`git ls-remote --exit-code --heads origin <branch>` — **exit 0 means it exists**; exit 2 is the normal "absent").
- **Refuse if `<branch>` is `main`** — a belt-and-braces check that costs one line and
  removes an entire category of catastrophe.
- `PublishingOptions`: `BranchPrefix` (default `fix/`), `Remote` (default `origin`).

### 3. `Loop.Engine.GitHub/Publishing` — the PR

**Files:** `PullRequestPublisher.cs`, `PullRequestBuilder.cs`

- `PullRequestBuilder` — **pure**, assembles `PullRequestContext` from the issue, analysis,
  verification result, and review. Title is `fix: <summary>`; `pr-lint.yml` fails without a
  Conventional Commits prefix.
- The body carries the review findings and the repair history. A reviewer should be able to
  see what the agent tried and what the Reviewer flagged without opening three artifacts.
- `PullRequestPublisher` — Octokit `PullRequest.Create`, base `main`, head the pushed branch.

### 4. Wire into the tick

**Files:** `IssuePollingService.cs`, `PipelineOptions.cs`, `DependencyInjection.cs`, `appsettings.json`

- `Pipeline:PublishPr` (default **false**), requires `VerifyFix`.
- Runs only on `VerificationResult.Succeeded`. On success print the PR URL; on refusal print
  why and continue — a failed publish must not discard a verified fix.

### 5. Tests

**Files:** `PullRequestBuilderTests.cs`, `GitPublisherTests.cs`, `Fakes/FakeGitPublisher.cs`, `Fakes/FakePullRequestPublisher.cs`

- `PullRequestBuilderTests` — body contains `Closes #8`; title starts with `fix:`; review
  findings and repair hypotheses appear.
- `GitPublisherTests` — against a **local temp repo with a file-path remote**, so push is
  real git with no network: refuses an existing branch, refuses `main`, and pushes a new
  branch successfully.
- Slug generation matches the `bug-loop` skill's rule (lowercase, non-alphanumerics to `-`,
  50 chars).
- **No network, no token, in any test.**

---

## Acceptance

| Issue requirement | Verified by |
| --- | --- |
| `git checkout -b` / `add` / `commit` / `push` | `GitPublisherTests` against a local remote |
| Create the PR | `FakePullRequestPublisher` + one manual run |
| PR template: Summary · Root Cause · Changes · Testing · Risk · Reviewer Notes | `PullRequestBuilderTests` — `PullRequestContext.RenderBody()` already emits all six |
| **Never merges** | No merge/approve method exists on either port |
| **Exit criterion** | `dotnet run` takes #8 to an open PR with no keystrokes; a human merges |

## Risks

- **Publishing something other than what was verified.** Mitigated by branching in the
  verified worktree. This is the same failure that silently deleted code earlier — worth
  naming twice.
- **Re-running on the same issue.** Refuse, never force-push. A human may be mid-review.
- **A green PR invites relaxing the no-merge rule.** It will look safe, repeatedly, right
  up until it is not. The rule is the product.

## Out of scope

Merging · closing issues by hand · updating an existing PR · labels · reviewers ·
fixing #8 manually.

## Migration

None — no entity changes.
