#!/usr/bin/env bash
#
# pick-work.sh — decide what the bug loop should do on this tick.
#
# Makes GitHub the single source of truth: no local state file, no lock. Every
# decision is derived from open issues + open PRs, so the loop survives restarts,
# machine changes, and a human editing things in the browser mid-flight.
#
# Prints KEY=value lines on stdout. Exits 0 with ACTION=idle when there is no
# work; exits 1 only when the environment is broken (unauthenticated, no repo),
# so the caller can tell "nothing to do" apart from "I am broken".
#
# Only `gh` is required — all JSON is shaped by gh's embedded jq (`--jq`), never
# by a standalone jq binary, which is not installed on this machine.
#
# Env overrides:
#   BUG_LOOP_REPO          owner/name          (default: repo of the cwd)
#   BUG_LOOP_LABEL         issue label to hunt (default: bug)
#   BUG_LOOP_BLOCKED_LABEL PR label meaning "stop touching this" (default: bug-loop-blocked)
#   BUG_LOOP_MAX_ATTEMPTS  fix cycles before escalating to a human (default: 5)

set -uo pipefail

repo="${BUG_LOOP_REPO:-}"
label="${BUG_LOOP_LABEL:-bug}"
blocked_label="${BUG_LOOP_BLOCKED_LABEL:-bug-loop-blocked}"
max_attempts="${BUG_LOOP_MAX_ATTEMPTS:-5}"

die() { echo "ERROR=$1" >&2; exit 1; }

# Probe the API rather than `gh auth status`: status exits non-zero when *any*
# configured account has a stale token, even if the active one is perfectly fine.
gh_user="$(gh api user --jq .login 2>/dev/null)" || die "gh-not-authenticated"

if [[ -z "$repo" ]]; then
  repo="$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>/dev/null)" \
    || die "no-repo-detected"
fi

slugify() {
  printf '%s' "$1" \
    | tr '[:upper:]' '[:lower:]' \
    | sed -e 's/[^a-z0-9]\+/-/g' -e 's/^-\+//' -e 's/-\+$//' \
    | cut -c1-50 \
    | sed -e 's/-\+$//'
}

emit_common() {
  echo "REPO=$repo"
  echo "ACTOR=$gh_user"
  echo "MAX_ATTEMPTS=$max_attempts"
}

# ---------------------------------------------------------------------------
# 1. Every PR the loop has ever opened, in one call, as TSV.
#    The branch name `fix/<issue>-<slug>` is the join key between an issue and
#    its PR — which is why the loop can rebuild all of its state from GitHub.
#    Open PRs are resume candidates; PRs in any state mark their issue "taken".
# ---------------------------------------------------------------------------
export BLOCKED_LABEL="$blocked_label"   # read back as $ENV.BLOCKED_LABEL inside --jq
pr_rows="$(gh pr list --repo "$repo" --state all --limit 60 \
  --json number,url,headRefName,isDraft,state,labels,statusCheckRollup \
  --jq '
    .[]
    | select(.headRefName | test("^fix/[0-9]+-"))
    | [
        (.headRefName | capture("^fix/(?<n>[0-9]+)-").n),
        (.number | tostring),
        .url,
        .headRefName,
        .state,
        (.isDraft | tostring),
        (((.labels // []) | map(.name) | index($ENV.BLOCKED_LABEL)) != null | tostring),
        (
          ((.statusCheckRollup // []) | map(select(. != null))) as $c
          | if ($c | length) == 0 then "none"
            elif ($c | map(select((.status // "COMPLETED") != "COMPLETED")) | length) > 0 then "pending"
            elif ($c | map(. as $e | select(["FAILURE","TIMED_OUT","CANCELLED","ACTION_REQUIRED","ERROR","STARTUP_FAILURE"] | index($e.conclusion // $e.state // ""))) | length) > 0 then "failing"
            else "passing" end
        )
      ]
    | @tsv
  ' 2>/dev/null)" || die "pr-list-failed"

# Issue numbers that already have a PR of any state — never start those twice.
taken=" "
while IFS=$'\t' read -r issue _rest; do
  [[ -n "${issue:-}" ]] && taken+="$issue "
done <<< "$pr_rows"

# ---------------------------------------------------------------------------
# 2. A PR mid-flight always outranks starting new work — one bug at a time.
#    "Needs work" = still a draft, or checks are anything other than green.
# ---------------------------------------------------------------------------
r_issue= r_pr= r_url= r_branch= r_draft= r_checks=
while IFS=$'\t' read -r issue pr url branch state draft blocked checks; do
  [[ -n "${issue:-}" ]]         || continue
  [[ "$state"   == "OPEN"  ]]   || continue
  [[ "$blocked" == "false" ]]   || continue
  [[ "$draft" == "true" || "$checks" != "passing" ]] || continue
  r_issue=$issue r_pr=$pr r_url=$url r_branch=$branch r_draft=$draft r_checks=$checks
  break
done <<< "$pr_rows"

if [[ -n "$r_issue" ]]; then
  # CI still running: the answer is patience, not another push.
  if [[ "$r_checks" == "pending" ]]; then
    emit_common
    echo "ACTION=wait"
    echo "ISSUE=$r_issue"
    echo "PR=$r_pr"
    echo "PR_URL=$r_url"
    echo "BRANCH=$r_branch"
    echo "CHECKS=$r_checks"
    exit 0
  fi

  # The attempt counter lives in PR comments, so it survives anything local.
  attempts="$(gh pr view "$r_pr" --repo "$repo" --json comments \
    --jq '[.comments[] | select(.body | contains("<!-- bug-loop:attempt -->"))] | length' \
    2>/dev/null)" || attempts=0

  issue_title="$(gh issue view "$r_issue" --repo "$repo" --json title --jq .title 2>/dev/null)" || issue_title=''

  emit_common
  if (( ${attempts:-0} >= max_attempts )); then
    echo "ACTION=escalate"
  else
    echo "ACTION=resume"
  fi
  echo "ISSUE=$r_issue"
  echo "ISSUE_TITLE=$issue_title"
  echo "ISSUE_URL=https://github.com/$repo/issues/$r_issue"
  echo "PR=$r_pr"
  echo "PR_URL=$r_url"
  echo "BRANCH=$r_branch"
  echo "CHECKS=$r_checks"
  echo "DRAFT=$r_draft"
  echo "ATTEMPTS=${attempts:-0}"
  exit 0
fi

# ---------------------------------------------------------------------------
# 3. Nothing in flight — take the oldest open bug issue with no PR yet.
# ---------------------------------------------------------------------------
issue_rows="$(gh issue list --repo "$repo" --state open --label "$label" --limit 50 \
  --json number,title,url \
  --jq 'sort_by(.number) | .[] | [(.number | tostring), .title, .url] | @tsv' \
  2>/dev/null)" || die "issue-list-failed"

while IFS=$'\t' read -r num title url; do
  [[ -n "${num:-}" ]] || continue
  [[ "$taken" == *" $num "* ]] && continue
  emit_common
  echo "ACTION=start"
  echo "ISSUE=$num"
  echo "ISSUE_TITLE=$title"
  echo "ISSUE_URL=$url"
  echo "BRANCH=fix/$num-$(slugify "$title")"
  echo "ATTEMPTS=0"
  exit 0
done <<< "$issue_rows"

emit_common
echo "ACTION=idle"
