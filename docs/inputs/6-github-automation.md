# Input: #6 — Phase 5: GitHub automation, issue to open PR

## Issue

<https://github.com/ndaonguyen/LoopEngineering/issues/6>

The last phase. Take the verified fix from Phase 4 and put it on GitHub: branch, commit,
push, open a pull request. Then stop — a human reviews and merges.

Everything upstream now works end to end. The most recent run on
[#8](https://github.com/ndaonguyen/LoopEngineering/issues/8) went investigate → plan →
code → build → test → review, green on the first verification attempt. This phase is the
last hop, and it is mostly plumbing rather than judgement.

## Design / Reference Links

- [docs/bug-loop-roadmap.md](../bug-loop-roadmap.md) — **Phase 5** is authoritative for
  scope and exit criterion.
- [docs/bug-loop-proposal.md](../bug-loop-proposal.md) — section *8. GitHub Agent*.
- `source/Loop.Engine.Core/Model/PullRequestContext.cs` — **already exists from Phase 1**,
  with `RenderBody()` producing `Closes #<n>` plus all six template sections. This is what
  it was written for; do not redesign it without a reason.
- `source/Loop.Engine.Agents/Verification/FixWorktree.cs` — already creates and cleans up a
  worktree, applies edits, and refuses to run on a dirty tree.
- `source/Loop.Engine.GitHub/` — Octokit client and `GitHubOptions` are already wired.
- `.github/workflows/pr-lint.yml` — enforces Conventional Commits on PR titles.

## Brainstorming

**Decided — carried from earlier phases, all proven in code:**

- Write files from `CodeChangeSet.Edits`, never `git apply`. The diff's paths are
  workspace-relative (`a/original/…`) and were never meant to be applied; the edits carry
  full file contents.
- Everything happens in a worktree. The user's checkout and branch are never touched.
- Fail loudly with specifics. Every failure message in this project names the thing that
  went wrong; a push failure should say which remote and which branch.

**Open for the planner:**

- **Branch from the existing `FixWorktree` or a fresh one?** The verification worktree is
  detached at HEAD with the fix applied — arguably ready to branch from. But reusing it
  couples publishing to verification, and a fix that skipped verification would have no
  worktree at all.
- **What identity commits?** The token's user, or a configured author. Octokit pushes over
  HTTPS with the token; `git commit` needs `user.name`/`user.email` set in the worktree.
- **Branch naming.** The `bug-loop` skill uses `fix/<n>-<slug>`; matching it means both
  loops are legible in the same branch list, but also that they could collide.
- **What if the branch already exists?** A second run on the same issue must not silently
  force-push over the first.

**Ruled out, do not re-propose:**

- Merging, auto-merging, or approving. The roadmap and the issue are explicit.
- Closing the issue directly — `Closes #<n>` in the body does it on merge.
- Pushing to `main`, or to any branch the user is on.
- Opening a PR for an unverified fix. Phase 4 exists precisely so this cannot happen.

## Constraints & Non-goals

- **Never merge.** Green PR, human decision. This is the invariant the whole project rests
  on and the one most tempting to relax once everything works.
- **Never push to `main`** and never touch the user's working tree or current branch.
- **Only publish a fix that passed Phase 4.** `VerificationResult.Succeeded` gates it. A
  PR containing a fix nobody built is worse than no PR: it looks reviewed.
- **PR title needs a Conventional Commits prefix** — `pr-lint.yml` fails otherwise. Bug
  fixes are `fix:`.
- **Every test runs offline.** GitHub and git invocation must sit behind a seam a fake can
  satisfy, exactly as `IBuildRunner` did for the compiler.
- **Do not break Phases 1–4.** 124 tests currently green; keep them so.
- **Gated behind a config flag, default off**, like `GenerateFix` and `VerifyFix`. Each
  phase stays dormant until asked for.
- **Exit criterion:** `dotnet run` takes a real issue to an open PR with no human
  keystrokes — and a human still merges it. The test case is #8, which has been carried
  unfixed through three phases for exactly this moment.
