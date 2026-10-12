#!/usr/bin/env bash
# Backs up the game database to backups/<database>-<UTC timestamp>.dump, in
# PostgreSQL's custom format (restore it with scripts/db-restore.sh).
#
# The database is the game's only source of truth -- players, curator edits,
# overrides, settings and daily puzzles live nowhere else -- so take a backup
# before anything risky, and keep copies somewhere other than this PC.
#
# Usage (Git Bash, from anywhere in the repo):
#   scripts/db-backup.sh                      # the local Docker database
#   DB_NAME=other scripts/db-backup.sh        # another database on the local server
#   DATABASE_URL='postgresql://user:pass@host:5432/db' scripts/db-backup.sh
#                                             # any other server, e.g. production
#
# pg_dump runs inside the docker compose "postgres" container, so that
# container must be running (docker compose up -d postgres), but nothing
# needs installing locally. Its pg_dump must be at least the server's major
# version, so a newer production server needs a newer postgres image here.
set -euo pipefail
cd "$(dirname "$0")/.."

if [[ -n "${DATABASE_URL:-}" ]]; then
  target=(--dbname="$DATABASE_URL")
  label="$(basename "${DATABASE_URL%%\?*}")"
else
  label="${DB_NAME:-idtheathlete}"
  target=(-U idtheathlete --dbname="$label")
fi

mkdir -p backups
file="backups/${label}-$(date -u +%Y%m%dT%H%M%SZ).dump"
trap 'rm -f "$file"' ERR

docker compose exec -T postgres pg_dump --format=custom --no-owner --no-privileges "${target[@]}" > "$file"

if [[ ! -s "$file" ]]; then
  rm -f "$file"
  echo "Backup failed: pg_dump produced no output." >&2
  exit 1
fi
echo "Backed up '$label' to $file ($(du -h "$file" | cut -f1))."
