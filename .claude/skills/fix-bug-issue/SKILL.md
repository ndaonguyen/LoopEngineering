---
name: fix-bug-issue
description: Take one GitHub bug issue, open a fix/<n>-<slug> PR in an isolated worktree, then diagnose and fix it with TDD until CI is green. Use when the user wants a single bug issue driven end-to-end, or when the bug-loop skill dispatches a tick.
argument-hint: "<issue-url-or-number>"
---

# /fix-bug-issue

The **worker** of the bug loop. One issue in, one green PR out.

`bug-loop` decides *what* to work on; this skill does the work. It is safe to invoke
directly on a single issue — it does not need the loop.

**Non-negotiables:**

- Only **bugs**. Feature requests get declined in Step 2, not silently implemented.
- Never merge, never push to `main`, never force-push a branch you did not create.
- Never close the issue by hand — the PR body's `Closes #<n>` does that on merge.
- The PR is opened **early and as a draft**. Work happens in the open, and CI red on
  a draft is the loop's steering signal, not a failure.

---

## Step 0: Preflight

Run in parallel:

- `gh api user --jq .login` — must succeed. (Do **not** use `gh auth status`; it exits
  non-zero when any configured account has a stale token, even if the active one works.)
- `git status --porcelain` — the **main checkout** must be clean.
- `gh repo view --json nameWithOwner --jq .nameWithOwner` — record as `$REPO`.

If the caller has not already authorised unattended PR creation this session, ask once:

> This will push a branch and open a draft PR on `<repo>`. Proceed?

Wait for a clear yes. One approval covers the rest of the session's ticks.

---

## Step 1: Resolve the issue

Accept a URL or a bare number.

```bash
gh issue view <n> --repo $REPO --json number,title,body,url,state,labels,comments
```

- `state == CLOSED` → stop. Say so; do not resurrect closed work.
- Read the comments too. Repro steps and "actually it also happens when…" almost always
  live there, not in the body.

---

## Step 2: Is it actually a bug?

Decide **before** touching git. Treat it as a bug when either holds:

- It carries the `bug` label, **or**
- The body describes an **expected vs actual** divergence — a stack trace, a wrong
  value, a 500, a regression ("worked in 1.2, broken in 1.3").

Not a bug: feature requests, questions, refactor wishes, "add support for X".

If it is **not** a bug, stop cleanly:

```bash
gh issue comment <n> --repo $REPO --body "<!-- bug-loop:skipped -->
Skipped by the bug loop: this reads as a feature request rather than a defect
(no expected-vs-actual behaviour described). Re-label as \`bug\` with repro steps
if that's wrong."
```

Then report to the caller and exit. **Do not** create a branch.

If the issue *is* a bug but has **no reproduction path at all** — no stack trace, no
steps, no failing input — do not guess. Comment asking for a repro, and exit. A fix
without a red test is a guess wearing a commit message.

---

## Step 3: Isolate — take a worktree

The loop must never disturb the user's checkout. Every issue gets its own worktree:

```bash
git -C <main-repo> fetch origin
git -C <main-repo> worktree add -b fix/<n>-<slug> ../LoopEngineering-worktrees/fix-<n> origin/main
```

- `<slug>`: issue title, lowercased, non-alphanumerics collapsed to `-`, trimmed, max 50
  chars. `bug-loop`'s picker emits the exact `BRANCH=` value — use it verbatim when
  dispatched, so the branch stays the join key between issue and PR.
- **Resuming** an existing branch instead:
  ```bash
  git -C <main-repo> worktree add ../LoopEngineering-worktrees/fix-<n> fix/<n>-<slug>
  git -C <worktree> pull --ff-only
  ```
- If the worktree path already exists, reuse it; do not blow it away — it may hold
  in-progress instrumentation from the previous tick.

**Everything from here runs with the worktree as the working directory.**

---

## Step 4: Diagnose — recall first, then fan out

**Read `docs/bug-loop-learnings.md` before anything else.** It is the loop's memory of
debugging *this* codebase: known test seams, known dead ends, error messages that point
away from their cause. Pull forward every entry matching this bug's area or symptom class
and carry them into the next step — a known dead end is worth more than a known seam,
because it deletes hypotheses you were about to spend attempts on.

Then dispatch diagnosis to a sub-agent, so the noisy part of the work (reading half the
repo to find the failure path) does not crowd out the fix.

Spawn `Explore` (or `general-purpose`) with:

> Bug report: `<issue title + body + repro>`.
> Repo: Clean-Architecture .NET service — read `CLAUDE.md` first.
> Already known from previous bugs: `<the matching learnings, verbatim>`.
> Find the code path that produces this symptom. Report: the entry point, the files and
> line numbers on the path, the seam where a test could observe the failure, and the two
> or three most likely causes with evidence. Do not re-propose a hypothesis listed above
> as a dead end unless you have evidence that overturns it. Do not fix anything.

While it runs, read the issue's linked code yourself. When it reports back, **verify its
claims against the files** before trusting them — a sub-agent's confident wrong answer
costs more than no answer.

Then follow the **`diagnosing-bugs`** skill, Phases 1–4. Phase 1 is the real work: land a
**tight, red-capable command** — usually `dotnet test --filter <Name>` — that fails on
*this* bug and would pass once fixed. Do not proceed to a hypothesis without it.

---

## Step 5: Open the draft PR

As soon as the failing test exists, commit it and put the work in the open:

```bash
git add -A
git commit -m "test: reproduce #<n> — <one-line symptom>"
git push -u origin fix/<n>-<slug>

gh pr create --repo $REPO --draft \
  --base main \
  --head fix/<n>-<slug> \
  --title "fix: <imperative summary of the defect>" \
  --body "<body, see below>"
```

The title **must** carry a Conventional Commits prefix — `.github/workflows/pr-lint.yml`
enforces it on every edit. Bugs are `fix:`.

PR body:

```markdown
Closes #<n>

## Symptom
<what the user sees, in their words>

## Reproduction
`dotnet test --filter <TestName>` — red at <commit sha>.

## Root cause
_Pending — diagnosis in progress._

## Fix
_Pending._

---
🤖 Opened by the bug loop. Draft until CI is green; a human merges.
```

CI will go red on this first push. That is the point — the red is the contract for what
"done" means.

---

## Step 6: Fix — red, green, refactor

Invoke the **`tdd`** skill and drive it:

1. **Red** — already have it from Step 4.
2. **Green** — the smallest change that makes it pass. Match the surrounding slice's
   shape; `CLAUDE.md` names the patterns (vertical-slice CQRS, ports in Application,
   adapters in Infrastructure).
3. **Refactor** — only with the test green.

Then verify locally before spending CI:

```bash
dotnet build -c Release --nologo
dotnet test -c Release --nologo
```

If entities changed, add the migration (see `CLAUDE.md`) — one migration, before the
final push, and commit the generated files.

Commit in Conventional Commits form: `fix: …`, `test: …`, `refactor: …`.

---

## Step 7: Close the loop on CI

Push, then record the attempt and watch:

```bash
git push
gh pr comment <pr> --repo $REPO --body "<!-- bug-loop:attempt -->
Attempt <k>: <what changed and why>."
gh pr checks <pr> --repo $REPO --watch --fail-fast
```

The marker comment is the loop's **durable attempt counter** — `pick-work.sh` counts
these to decide when to escalate. Post exactly one per push; skipping it makes the loop
believe it has infinite retries.

**If CI fails**, pull the real reason rather than guessing:

```bash
gh run view --repo $REPO --log-failed
```

Then go back to Step 4 with the CI output as new evidence. Each cycle must change a
**hypothesis**, not just a line of code — if two consecutive attempts try variations of
the same idea, the idea is wrong; regenerate hypotheses.

**If CI is green**, continue to Step 8.

---

## Step 8: Review, then hand to a human

Before marking ready, get a second opinion. Spawn a sub-agent:

> Review the diff on branch `fix/<n>-<slug>` against issue #`<n>`. Two questions only:
> does the fix address the reported symptom's *root cause* rather than masking it, and
> does the regression test actually fail without the fix? Report concerns, do not edit.

Act on anything real.

### Write back to memory

**First, check whether you already did this on an earlier tick:**

```bash
git log origin/main..HEAD --oneline -- docs/bug-loop-learnings.md
```

Any commit there means this branch has already written its learning — skip straight to
**Hand off**. Without this check a tick that ends while CI re-runs comes back to Step 8
and writes the entry twice.

Otherwise, decide whether this bug taught the loop anything durable. Read
`docs/bug-loop-learnings.md` — the bar and the entry format are defined there. In short:
write an entry **only** if knowing it at Step 4 would have saved you time — a non-obvious
test seam, a dead-end hypothesis that cost attempts, a misleading error, or a structural
cause that will recur.

**Most fixes teach nothing durable, and no entry is the normal outcome.** Do not
paraphrase the fix or the issue; those already live in the PR and the issue. If an
existing entry covers this ground, *sharpen that one* instead of appending a near-
duplicate; if one proved wrong, correct or delete it here.

If you do write one, commit it on this branch so it ships inside the PR:

```bash
git add docs/bug-loop-learnings.md
git commit -m "docs: record bug-loop learning from #<n>"
git push
```

That is deliberate: the learning goes through the same human review as the fix, which is
the only thing stopping this file from accumulating confident nonsense. A learning
committed on a PR that never merges never lands — which is the correct outcome.

This push restarts CI. Wait for it before marking ready:

```bash
gh pr checks <pr> --repo $REPO --watch --fail-fast
```

A docs-only commit should not break a green build; if it does, something else is wrong —
go back to Step 7's failure path rather than marking ready anyway.

### Hand off

```bash
gh pr edit <pr> --repo $REPO --body "<updated body: Root cause + Fix filled in>"
gh pr ready <pr> --repo $REPO
```

Finish by cleaning up the isolation:

```bash
git -C <main-repo> worktree remove ../LoopEngineering-worktrees/fix-<n>
```

Only remove it once the PR is green and ready — a removed worktree with unpushed work
is unrecoverable.

**Stop there.** The loop does not merge. A human reviews and merges; the `Closes #<n>`
closes the issue.

---

## Step 9: When to give up

Escalate rather than thrash. Trigger on any of:

- Attempt count reaches `MAX_ATTEMPTS` (default 5).
- Two consecutive attempts produce the *same* CI failure.
- The fix would require a change the issue never asked for (schema redesign, dependency
  bump, auth rework).

To escalate:

```bash
gh label create bug-loop-blocked --repo $REPO --color d73a4a \
  --description "Bug loop needs a human" 2>/dev/null || true
gh pr edit <pr> --repo $REPO --add-label bug-loop-blocked
gh pr comment <pr> --repo $REPO --body "<!-- bug-loop:blocked -->
Escalating after <k> attempts. What I know: <root-cause evidence>. What I tried:
<hypotheses, each with the observation that killed it>. Why I'm stuck: <the specific
unknown>. Suggested next step: <concrete>."
```

The `bug-loop-blocked` label makes the picker skip this PR permanently, so the loop
moves to the next bug instead of grinding. Leave the branch and worktree intact for the
human. A well-written stuck report is a successful outcome; a wrong fix is not.

**Do not write to `docs/bug-loop-learnings.md` when escalating.** An escalated PR never
merges, so the entry would never reach `main` — and a lesson drawn from a bug you did not
actually solve is exactly the kind of plausible-but-unverified claim that poisons the
file. The stuck report *is* the memory for this one; put the dead ends there, in full.
