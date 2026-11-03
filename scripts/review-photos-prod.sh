#!/usr/bin/env bash
# Suggests photos for PRODUCTION players without one, with the photo review
# tool (tools/photo_review.py), and the steps around it done for you:
#   1. asks for production's database URL and admin key (hidden input, kept
#      only for this run: nothing is saved or left set in your terminal)
#   2. wakes the production API, which the photos are written through
#   3. backs production up to backups/ (stops here if that fails)
#   4. runs the review: looks the players up on Wikidata (free, no AI), opens
#      the suggestions in your browser, and writes the ones you accept, after
#      you type production's database name
#   5. offers to copy production into your local dev database
#
# Usage (Git Bash, from anywhere in the repo):
#   scripts/review-photos-prod.sh tennis-men              # every player without a photo
#   scripts/review-photos-prod.sh cricket-men-international --limit 50
#
# Needs the local Docker postgres container running (the backup runs pg_dump
# in it) and the tools' Python packages (see the top of
# tools/roster_research_agent.py). Production's External Database URL is on
# Render: the database's Connect menu, External.
#
# PROD_API_URL overrides the production API's address. DB_NAME picks the local
# database step 5 copies into (default: the dev database, idtheathlete).
set -euo pipefail
cd "$(dirname "$0")/.."

if [[ $# -eq 0 || "$1" == "-h" || "$1" == "--help" ]]; then
  sed -n '2,23p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
fi

api_url="${PROD_API_URL:-https://id-the-athlete-api.onrender.com}"
api_url="${api_url%/}"
python="${PYTHON:-python}"

# Python on Windows can fail to check Wikimedia's certificate against the
# system's store; Git's own CA bundle is kept up to date with Git.
git_bundle="/mingw64/etc/ssl/certs/ca-bundle.crt"
if [[ -z "${SSL_CERT_FILE:-}" && -f "$git_bundle" ]] && command -v cygpath >/dev/null; then
  export SSL_CERT_FILE="$(cygpath -w "$git_bundle")"
fi

# The tool's own argument check, so a mistyped tour name fails now.
(cd tools && "$python" -c 'import sys, photo_review as r; p = r.build_arg_parser(); p.prog = "scripts/review-photos-prod.sh"; p.parse_args(sys.argv[1:])' "$@")

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
  echo "The admin key is needed: photos are written through the Control Room's API." >&2
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
  echo "The API didn't answer, and the photos are written through it. Try again in a minute." >&2
  exit 1
fi
echo "The API is up."

echo "Backing up production first..."
DATABASE_URL="$db_url" bash scripts/db-backup.sh </dev/null

(
  cd tools
  ROSTER_DB_URL="$db_url" ROSTER_API_URL="$api_url" ROSTER_ADMIN_KEY="$admin_key" \
    "$python" photo_review.py "$@"
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
