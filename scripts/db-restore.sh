#!/usr/bin/env bash
# Restores a backup made by scripts/db-backup.sh, REPLACING everything in the
# target database with the backup's contents. Asks you to type the target
# database's name before doing anything.
#
# Usage (Git Bash, from anywhere in the repo):
#   scripts/db-restore.sh backups/<file>.dump                   # into the local Docker database
#   scripts/db-restore.sh backups/<file>.dump --skip-puzzle-history
#   DB_NAME=other scripts/db-restore.sh backups/<file>.dump     # another local database (must exist)
#   DATABASE_URL='postgresql://user:pass@host:5432/db' scripts/db-restore.sh backups/<file>.dump
#                                                               # any other server, e.g. production
#
# --skip-puzzle-history removes the restored daily puzzles afterwards, so the
# target's Past Challenges start fresh. Use it when copying the dev database
# into production for the first time; the API creates today's puzzle on start.
set -euo pipefail
cd "$(dirname "$0")/.."

usage="Usage: scripts/db-restore.sh <backup-file> [--skip-puzzle-history]"
file="${1:?$usage}"
skip_puzzles=false
if [[ "${2:-}" == "--skip-puzzle-history" ]]; then
  skip_puzzles=true
elif [[ -n "${2:-}" ]]; then
  echo "$usage" >&2
  exit 1
fi
if [[ ! -s "$file" ]]; then
  echo "No backup file at '$file'." >&2
  exit 1
fi

if [[ -n "${DATABASE_URL:-}" ]]; then
  target=(--dbname="$DATABASE_URL")
  label="$(basename "${DATABASE_URL%%\?*}")"
else
  label="${DB_NAME:-idtheathlete}"
  target=(-U idtheathlete --dbname="$label")
fi

echo "This REPLACES ALL DATA in database '$label' with the contents of $file."
$skip_puzzles && echo "Daily puzzle history from the backup will then be removed."
read -r -p "Type the database name ($label) to continue: " answer
if [[ "$answer" != "$label" ]]; then
  echo "Cancelled; nothing was changed."
  exit 1
fi

# --single-transaction: if anything fails, the target is left exactly as it was.
docker compose exec -T postgres pg_restore --clean --if-exists --no-owner --no-privileges \
  --single-transaction "${target[@]}" < "$file"

if $skip_puzzles; then
  docker compose exec -T postgres psql -v ON_ERROR_STOP=1 -q "${target[@]}" -c 'DELETE FROM "DailyPuzzles";'
fi
echo "Restored $file into '$label'."
