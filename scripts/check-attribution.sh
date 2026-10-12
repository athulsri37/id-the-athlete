#!/usr/bin/env bash
# Fails if a commit message in the given range, or the text in the PR_BODY
# environment variable, carries an AI attribution: a Co-authored-by or
# Claude-Session trailer, a "Generated with Claude Code" footer, or a Claude
# session link. The repository owner is the sole author (CLAUDE.md,
# Attribution). Text that only mentions these, mid-sentence, is fine.
#
# Usage (Git Bash):
#   scripts/check-attribution.sh                    # origin/main..HEAD
#   scripts/check-attribution.sh <base>..<head>
#   PR_BODY="$(gh pr view 123 --json body --jq .body)" scripts/check-attribution.sh
set -euo pipefail

range="${1:-origin/main..HEAD}"
pattern='^(co-authored-by|claude-session):|^[^[:alnum:]]*generated with \[?claude code|claude\.ai/code/session_'
found=0

if git log --no-merges --format=%B "$range" | grep -inE "$pattern"; then
  echo "A commit message in $range has the attribution line above; reword that commit." >&2
  found=1
fi

if [ -n "${PR_BODY:-}" ] && printf '%s\n' "$PR_BODY" | grep -inE "$pattern"; then
  echo "The PR description has the attribution line above; edit the description." >&2
  found=1
fi

if [ "$found" -eq 0 ]; then
  echo "No attribution lines found."
fi
exit "$found"
