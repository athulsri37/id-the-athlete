# ID the Athlete

A multi-sport, Wordle-style athlete deduction game. React/TypeScript frontend, C#/ASP.NET Core backend, PostgreSQL, Docker Compose. Currently covers 4 tours: Men's/Women's Tennis, Men's/Women's International Cricket.

## Architecture

Core schema is sport-agnostic by design: `Sport → Player → AttributeDefinition → PlayerAttributeValue`. A sport's clue columns are rows, so the schema and frontend need no changes for a new sport. Scoring rules (difficulty formulas, what counts as "close") live per sport family: `Sports.Family` picks an `ISportRules` implementation in `backend/IdTheAthlete.Api/Rules/` (`TennisRules`, `CricketRules`), registered in `Program.cs`.

- New tour of an existing family (e.g. a domestic Cricket league): a migration inserting its `Sports` row (right `Family`, a `ShortName` for its tour-select button) and `AttributeDefinitions` rows, like `AddReferenceData`, plus anything that family's rules need per tour (Cricket: an entry in `CricketRules.ThresholdsByTour`) and a roster agent profile. No frontend change: the home and tour screens come from `GET /api/sports`.
- New sport: the same kind of migration, a new `ISportRules` class registered in `Program.cs`, an entry in `frontend/src/config/sportFamilies.ts` (home button, title wording, win emoji, clue legend examples) and a roster agent profile. Without the frontend entry the sport still works, with plain defaults.
- Never branch on a sport's slug outside its family's rules class. The app refuses to start if any sport's family has no rules or its tour isn't supported, so a gap fails loudly instead of silently scoring with another sport's rules.

## Data

The database is the single source of truth: players, curator edits, overrides, settings and daily puzzles exist nowhere else (there are no seed files). Migrations create only the reference data (sports, clue columns, default settings) on an empty database. Back up with `scripts/db-backup.sh`, restore with `scripts/db-restore.sh` (Git Bash; see the README's "Player Data & Backups"). Never test destructive changes against the real dev database: restore a backup into a throwaway database (`DB_NAME=...`) instead.

## Setup

```
docker compose up -d postgres
cd backend/IdTheAthlete.Api
dotnet ef database update
dotnet run
```
New terminal:
```
cd frontend
npm run dev
```
App runs at `http://localhost:5174`. A fresh database has the sports but no players: restore a backup (`scripts/db-restore.sh backups/<file>.dump`) to get them.

Requires `dotnet user-secrets` set for `GameTokens:SigningKey` (signs daily-game guess counts; 32+ characters; optional in Development, required elsewhere or the app won't start), `Anthropic:ApiKey` (AI trivia) and `AdminKeys:<name>` (a key-protected internal admin interface, not linked from public UI, don't reference its exact route in committed files).

## Testing

```
cd backend/IdTheAthlete.Api.Tests
dotnet test
```
```
cd frontend
npm test
npm run test:e2e   # requires both dev servers already running
```
```
cd tools
python -m unittest test_roster_validation   # roster agent's value checks
```

## Adding player data

Players are added with the roster research agent (`tools/roster_research_agent.py`, run by the user since it uses their Claude subscription): it researches candidates, checks every value against the sport's columns and existing values, shows each player's difficulty tier (`--tiers`, default leaning Medium), and writes only the players the user accepts, after confirming the target database. Don't write player rows by hand.

Standing rules when adding players:
- Count the candidate list explicitly before starting research, this project has repeatedly miscounted 30 as 31
- Present the full candidate list and wait for explicit approval before starting any research or data collection
- Player names are unique per sport (the `(SportId, Name)` index). If a new player is a different person with the same name as an existing player in that sport, store a disambiguated name: country in parentheses as written in the `country` field, e.g. `John Smith (New Zealand)`, or birth year if they also share a country, e.g. `John Smith (b. 1985)`. The same name in a different sport needs nothing extra

## Known gotchas

**When you make a genuinely avoidable error during a task, one that would likely recur for a future session working on something different, add a concise line here before finishing, rather than letting the lesson disappear when the session ends.** Not every mistake qualifies, a one-off environment quirk or something already covered below doesn't need a new line. A wrong assumption about how something actually behaves, confirmed wrong only after real back-and-forth, does.

- `PlayerAttributeValues.IsManuallyEdited` protects admin-tool edits from being silently overwritten by future seed runs (every seed file's upsert only updates a row when this flag is false). Don't remove or bypass this without understanding why it exists, it was added after a real incident where manual edits were getting clobbered.
- `Players.IsOverridden` / `Players.DifficultyOverride` let a player's computed difficulty tier be manually overridden (e.g., a player famous for one moment despite modest stats). New players are always written with `IsOverridden = false, DifficultyOverride = NULL` (the roster agent does this); overrides are set through the admin interface.
- Difficulty tier formulas differ by sport AND gender for Cricket (Men's and Women's use different thresholds), but are shared across gender for Tennis. Don't assume one formula applies everywhere.
- Attribute key names must match exactly what's in the `AttributeDefinitions` table, verify rather than assume (e.g., it's `grand_slam_titles` and `turned_pro_year`, not `grand_slams` or `turned_pro`).
- `tools/` contains standalone Python scripts (a roster research agent), a different language and set of conventions from the rest of the codebase, don't assume it follows the same patterns as the C#/React app.
- The `claude-agent-sdk` Python package's `ClaudeAgentOptions` has no `allow_dangerously_skip_permissions` parameter (at least as of version 0.2.156), `permission_mode="bypassPermissions"` alone is sufficient for headless tool approval. Verify exact parameters via `inspect.signature()` against the actually installed version rather than trusting external documentation, which may describe a different version or language binding.
- The same SDK's `allowed_tools` is a plain list of tool name strings with no way to attach extra configuration like `allowed_domains`, unlike the raw Messages API's tool definition format, which does support this directly. If a tool needs domain restriction through this SDK, a prompt-level instruction is the fallback, not a technical guarantee.
- Never add Co-Authored-By, "Generated with Claude Code", or Claude-Session trailers to commit messages or PR descriptions. The repository owner is the sole author.
- `db.Database.Migrate()` in `Program.cs` creates the target database if it doesn't exist instead of failing, so a connection string with a mistyped database name starts the app normally against a new, empty database (schema, no players). When pointing at a database that may not exist, check `pg_database` afterwards and drop anything created by accident.
