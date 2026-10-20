#!/usr/bin/env bash
# Adds players to PRODUCTION with the roster research agent, with the steps
# around it done for you:
#   1. asks for production's database URL and admin key (hidden input, kept
#      only for this run: nothing is saved or left set in your terminal)
#   2. wakes the production API, so the agent can show difficulty tiers
#   3. backs production up to backups/ (stops here if that fails)
#   4. runs the agent against production: you review the players as usual, and
#      writing them needs production's database name typed, not just "yes"
#   5. offers to copy production into your local dev database, so dev keeps
#      matching it
#
# Usage (Git Bash, from anywhere in the repo), taking the agent's arguments:
#   scripts/add-players-prod.sh tennis-men 20
#   scripts/add-players-prod.sh cricket-women-international 10 --tiers medium,hard
#
# Needs what the agent and scripts/db-backup.sh need: the local Docker
# postgres container running (the backup runs pg_dump in it), the agent's
# Python packages, and CLAUDE_CODE_OAUTH_TOKEN set (see the top of
# tools/roster_research_agent.py). Production's External Database URL is on
# Render: the database's Connect menu, External.
#
# PROD_API_URL overrides the production API's address. DB_NAME picks the local
# database step 5 copies into (default: the dev database, idtheathlete).
set -euo pipefail
cd "$(dirname "$0")/.."

if [[ $# -eq 0 || "$1" == "-h" || "$1" == "--help" ]]; then
  sed -n '2,24p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
fi

api_url="${PROD_API_URL:-https://id-the-athlete-api.onrender.com}"
api_url="${api_url%/}"
python="${PYTHON:-python}"

# Checked now rather than by the agent after the backup, so a missing token
# doesn't cost a backup. The agent checks again.
if [[ -n "${ANTHROPIC_API_KEY:-}" ]]; then
  echo "ANTHROPIC_API_KEY is set, which would bill your API balance instead of your" >&2
  echo "subscription. Run 'unset ANTHROPIC_API_KEY' and try again." >&2
  exit 1
fi
if [[ -z "${CLAUDE_CODE_OAUTH_TOKEN:-}" ]]; then
  echo "CLAUDE_CODE_OAUTH_TOKEN isn't set: see the top of tools/roster_research_agent.py." >&2
  exit 1
fi
# The agent's own argument check, run before anything is asked or backed up,
# so a mistyped tour name fails now rather than after the backup.
(cd tools && "$python" -c 'import sys, roster_research_agent as a; p = a.build_arg_parser(); p.prog = "scripts/add-players-prod.sh"; p.parse_args(sys.argv[1:])' "$@")

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
read -r -s -p "Production admin key, for the tier column (hidden; Enter to skip): " admin_key
echo
admin_key="$(tr -d '[:space:]' <<<"$admin_key")"

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
$awake && echo "The API is up." || echo "The API didn't answer: carrying on, but the tier column will show '?'."

echo "Backing up production first..."
DATABASE_URL="$db_url" bash scripts/db-backup.sh </dev/null

echo "Starting the roster agent against production..."
(
  cd tools
  ROSTER_DB_URL="$db_url" ROSTER_API_URL="$api_url" ROSTER_ADMIN_KEY="$admin_key" \
    "$python" roster_research_agent.py "$@"
)

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
