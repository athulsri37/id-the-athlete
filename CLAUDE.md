# ID the Athlete

A multi-sport, Wordle-style athlete deduction game. React/TypeScript frontend, C#/ASP.NET Core backend, PostgreSQL, Docker Compose. Currently covers 4 tours: Men's/Women's Tennis, Men's/Women's International Cricket.

## Architecture

Core schema is sport-agnostic by design: `Sport → Player → AttributeDefinition → PlayerAttributeValue`. This is what let Cricket be added as pure data, with zero schema or backend code changes. When adding a new sport variant, this pattern should hold, don't special-case a new sport in code if it can instead be new rows in these tables.

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
App runs at `http://localhost:5174`.

Requires `dotnet user-secrets` set for `Anthropic:ApiKey` (AI trivia) and `AdminKeys:<name>` (a key-protected internal admin interface, not linked from public UI, don't reference its exact route in committed files).

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

## Adding player data

Seed files live in `backend/SeedTool/SeedData/<Sport>/<Tour>/players-batch-NN.sql`. Use the most recent existing batch file in the target sport/tour as the exact pattern to follow, don't write the INSERT structure from scratch.

Standing rules for new batches:
- Lean Medium/Hard tier (avoid adding more Easy-tier legends, most rosters already skew that way)
- Count the candidate list explicitly before starting research, this project has repeatedly miscounted 30 as 31
- Present the full candidate list and wait for explicit approval before starting any research or data collection

## Known gotchas

- `PlayerAttributeValues.IsManuallyEdited` protects admin-tool edits from being silently overwritten by future seed runs (every seed file's upsert only updates a row when this flag is false). Don't remove or bypass this without understanding why it exists, it was added after a real incident where manual edits were getting clobbered.
- `Players.IsOverridden` / `Players.DifficultyOverride` let a player's computed difficulty tier be manually overridden (e.g., a player famous for one moment despite modest stats). New seed rows must always set `IsOverridden = false, DifficultyOverride = NULL`, existing overrides are applied through the admin interface, not by editing seed files.
- Difficulty tier formulas differ by sport AND gender for Cricket (Men's and Women's use different thresholds), but are shared across gender for Tennis. Don't assume one formula applies everywhere.
- Attribute key names must match exactly what's in the `AttributeDefinitions` table, verify rather than assume (e.g., it's `grand_slam_titles` and `turned_pro_year`, not `grand_slams` or `turned_pro`).
- `tools/` contains standalone Python scripts (a roster research agent), a different language and set of conventions from the rest of the codebase, don't assume it follows the same patterns as the C#/React app.
