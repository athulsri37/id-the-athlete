#!/usr/bin/env bash
# Fails on an AI attribution: a Co-authored-by or Claude-Session trailer, a
# "Generated with Claude Code" footer, or a Claude session link. The
# repository owner is the sole author (CLAUDE.md, Attribution). Text that
# only mentions these, mid-sentence, is fine.
#
# PRs are squash-merged with the PR title and description as the commit
# message, so the description is what reaches main; CI checks it with
# --description. Checking commit messages is a local courtesy.
#
# Usage (Git Bash):
#   scripts/check-attribution.sh                    # commits in origin/main..HEAD
#   scripts/check-attribution.sh <base>..<head>     # commits in that range
#   PR_BODY="$(gh pr view 123 --json body --jq .body)" scripts/check-attribution.sh --description
set -euo pipefail

pattern='^(co-authored-by|claude-session):|^[^[:alnum:]]*generated with \[?claude code|claude\.ai/code/session_'

if [ "${1:-}" = "--description" ]; then
  if printf '%s\n' "${PR_BODY:-}" | grep -inE "$pattern"; then
    echo "The PR description has the attribution line above; edit the description." >&2
    exit 1
  fi
  echo "No attribution lines in the PR description."
  exit 0
fi

range="${1:-origin/main..HEAD}"
if git log --no-merges --format=%B "$range" | grep -inE "$pattern"; then
  echo "A commit message in $range has the attribution line above; reword that commit before pushing." >&2
  exit 1
fi
echo "No attribution lines in the commits in $range."
