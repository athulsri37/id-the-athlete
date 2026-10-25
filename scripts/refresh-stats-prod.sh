#!/usr/bin/env bash
# Refreshes active players' stats in PRODUCTION with the stat refresh tool
# (tools/stat_refresh_agent.py), with the steps around it done for you:
#   1. asks for production's database URL and admin key (hidden input, kept
#      only for this run: nothing is saved or left set in your terminal)
#   2. wakes the production API, which apply writes through
#   3. research: the agent looks up current stats for the tour's active
#      players and saves them (reads production only; uses your subscription)
#   4. asks whether to apply now; if so, backs production up to backups/
#      (stops here if that fails) and runs apply: you review the changes, and
#      writing them needs production's database name typed
#   5. offers to copy production into your local dev database
#
# Usage (Git Bash, from anywhere in the repo):
#   scripts/refresh-stats-prod.sh tennis-men --limit 40   # research 40 more players
#   scripts/refresh-stats-prod.sh tennis-men              # research all the rest
#   scripts/refresh-stats-prod.sh tennis-men --apply-only # write held players later
# A refresh covers every active player and can be spread over several runs
# (sessions) with --limit: each run asks whether to continue it (c) or start
# a new one (n), and you can apply after any run.
#
# Needs the local Docker postgres container running (the backup runs pg_dump
# in it), the agent's Python packages, and, unless --apply-only,
# CLAUDE_CODE_OAUTH_TOKEN set (see the top of tools/roster_research_agent.py).
# Production's External Database URL is on Render: the database's Connect
# menu, External.
#
# PROD_API_URL overrides the production API's address. DB_NAME picks the local
# database step 5 copies into (default: the dev database, idtheathlete).
set -euo pipefail
cd "$(dirname "$0")/.."

if [[ $# -eq 0 || "$1" == "-h" || "$1" == "--help" ]]; then
  sed -n '2,29p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
fi

tour="$1"
shift
apply_only=false
research_args=()
for arg in "$@"; do
  if [[ "$arg" == "--apply-only" ]]; then
    apply_only=true
  else
    research_args+=("$arg")
  fi
done
if $apply_only && ((${#research_args[@]} > 0)); then
  echo "--apply-only takes no other options." >&2
  exit 1
fi

api_url="${PROD_API_URL:-https://id-the-athlete-api.onrender.com}"
api_url="${api_url%/}"
python="${PYTHON:-python}"

# Checked now rather than by the agent, so a missing token fails before any
# question is asked. The agent checks again.
if ! $apply_only; then
  if [[ -n "${ANTHROPIC_API_KEY:-}" ]]; then
    echo "ANTHROPIC_API_KEY is set, which would bill your API balance instead of your" >&2
    echo "subscription. Run 'unset ANTHROPIC_API_KEY' and try again." >&2
    exit 1
  fi
  if [[ -z "${CLAUDE_CODE_OAUTH_TOKEN:-}" ]]; then
    echo "CLAUDE_CODE_OAUTH_TOKEN isn't set: see the top of tools/roster_research_agent.py." >&2
    exit 1
  fi
fi
# The tool's own argument check, so a mistyped tour name fails now.
(cd tools && "$python" -c 'import sys, stat_refresh_agent as a; p = a.build_arg_parser(); p.prog = "scripts/refresh-stats-prod.sh"; p.parse_args(sys.argv[1:])' research "$tour" ${research_args[@]+"${research_args[@]}"})

# docker compose exec reads standard input, so every docker call here (and the
# backups) gets </dev/null: otherwise it can swallow your answers to the prompts.
if ! docker compose exec -T postgres pg_isready -q </dev/null >/dev/null 2>&1; then
  echo "The local postgres container isn't running (the backup needs it):" >&2
  echo "  docker compose up -d postgres" >&2
  exit 1
fi

read -r -s -p "Production External Database URL (hidden): " db_url
echo
# Pasting from a password manager can bring a space or line break along, which
# would become part of the database name.
db_url="$(tr -d '[:space:]' <<<"$db_url")"
if [[ ! "$db_url" =~ ^postgres(ql)?:// ]]; then
  echo "That doesn't look like a database URL (it should start with postgresql://)." >&2
  exit 1
fi
read -r -s -p "Production admin key (hidden): " admin_key
echo
admin_key="$(tr -d '[:space:]' <<<"$admin_key")"
if [[ -z "$admin_key" ]]; then
  echo "The admin key is needed: apply writes through the Control Room's API." >&2
  exit 1
fi

echo "Waking the production API at $api_url (it sleeps when idle; up to 90 seconds)..."
awake=false
deadline=$((SECONDS + 90))
while ((SECONDS < deadline)); do
  if curl -fsS --max-time 10 "$api_url/api/health" >/dev/null 2>&1; then
    awake=true
    break
  fi
  sleep 5
done
if ! $awake; then
  echo "The API didn't answer, and apply writes through it. Try again in a minute." >&2
  exit 1
fi
echo "The API is up."

run_tool() {
  (
    cd tools
    ROSTER_DB_URL="$db_url" ROSTER_API_URL="$api_url" ROSTER_ADMIN_KEY="$admin_key" \
      "$python" stat_refresh_agent.py "$@"
  )
}

if ! $apply_only; then
  run_tool research "$tour" ${research_args[@]+"${research_args[@]}"}
  echo
  read -r -p "Review and apply the research now? (y/N) " answer
  if [[ ! "$answer" =~ ^[Yy] ]]; then
    echo "Not applied. Later: scripts/refresh-stats-prod.sh $tour --apply-only"
    exit 0
  fi
fi

echo "Backing up production first..."
DATABASE_URL="$db_url" bash scripts/db-backup.sh </dev/null

run_tool apply "$tour"

echo
read -r -p "Copy production into your local dev database now, so dev matches it? (y/N) " answer
if [[ "$answer" =~ ^[Yy] ]]; then
  backup_output="$(DATABASE_URL="$db_url" bash scripts/db-backup.sh </dev/null)"
  echo "$backup_output"
  file="$(sed -n "s/^Backed up '.*' to \(backups\/[^ ]*\.dump\).*/\1/p" <<<"$backup_output")"
  # Without DATABASE_URL the restore targets the local server; it asks you to
  # type the local database's name before replacing anything.
  env -u DATABASE_URL bash scripts/db-restore.sh "$file"
fi
