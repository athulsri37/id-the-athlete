# ID the Athlete

A multi-sport, Wordle-style athlete deduction game. React/TypeScript frontend, C#/ASP.NET Core backend, PostgreSQL, Docker Compose. Currently covers 4 tours: Men's/Women's Tennis, Men's/Women's International Cricket.

## Architecture

Core schema is sport-agnostic by design: `Sport → Player → AttributeDefinition → PlayerAttributeValue`. A sport's clue columns are rows, so the schema and frontend need no changes for a new sport. Scoring rules (difficulty formulas, what counts as "close") live per sport family: `Sports.Family` picks an `ISportRules` implementation in `backend/IdTheAthlete.Api/Rules/` (`TennisRules`, `CricketRules`), registered in `Program.cs`.

- New tour of an existing family (e.g. a domestic Cricket league): a migration inserting its `Sports` row (right `Family`, a `ShortName` for its tour-select button) and `AttributeDefinitions` rows, like `AddReferenceData`, plus anything that family's rules need per tour (Cricket: an entry in `CricketRules.ThresholdsByTour`) and a roster agent profile. No frontend change: the home and tour screens come from `GET /api/sports`.
- New sport: the same kind of migration, a new `ISportRules` class registered in `Program.cs`, an entry in `frontend/src/config/sportFamilies.ts` (home button, title wording, win emoji, clue legend examples) and a roster agent profile. Without the frontend entry the sport still works, with plain defaults.
- Errors: services throw `NotFoundException` (resource named in the route is missing → 404) or `InvalidRequestException` (bad body/query value or broken game rule → 400) from `backend/IdTheAthlete.Api/Errors/`, and `ClientErrorExceptionHandler` turns them into problem responses. Controllers never catch exceptions. Anything else, including `InvalidOperationException`, is treated as a server bug: a logged 500 with no details.
- Never branch on a sport's slug outside its family's rules class. The app refuses to start if any sport's family has no rules or its tour isn't supported, so a gap fails loudly instead of silently scoring with another sport's rules.

## Data

The database is the single source of truth: players, curator edits, overrides, settings and daily puzzles exist nowhere else (there are no seed files). Migrations create only the reference data (sports, clue columns, default settings) on an empty database. Back up with `scripts/db-backup.sh`, restore with `scripts/db-restore.sh` (Git Bash; see the README's "Player Data & Backups"). Never test destructive changes against the real dev database: restore a backup into a throwaway database (`DB_NAME=...`) instead.

Game stats (`GameRecords`, one row per game, see the README's "Game Stats") are anonymous by design: a random ID per game and nothing that identifies a person. Adding an IP address, device or cookie ID, or any account link makes them personal data, so it's the owner's decision, not an implementation detail. Recording stats must never fail a guess or hint.

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
Requires Node 24 (`frontend/.nvmrc`, also used by CI and the frontend image). App runs at `http://localhost:5174`. A fresh database has the sports but no players: restore a backup (`scripts/db-restore.sh backups/<file>.dump`) to get them.

Requires `dotnet user-secrets` set for `GameTokens:SigningKey` (signs daily-game guess counts; 32+ characters; optional in Development, required elsewhere or the app won't start), `Anthropic:ApiKey` (AI trivia) and `AdminKeys:<name>` (a key-protected internal admin interface, not linked from public UI, don't reference its exact route in committed files).

## Testing

```
cd backend/IdTheAthlete.Api.Tests
dotnet test
```
```
cd frontend
npm test
npm run test:e2e   # requires both dev servers already running; E2E_BASE_URL=http://localhost:5175 for the test ports
```
```
cd tools
python -m unittest test_roster_validation   # roster agent's value checks
```

## Working process

Applies to every agent session, local or cloud.

### When to stop and ask
Work autonomously. Stop only for decisions that need the owner's product judgement:
- How the game behaves for players: rules, scoring, what's shown or hidden, difficulty, copy.
- Anything that costs money (paid API calls, the owner's Claude subscription).
- Writing to a real database, deploying, force-pushing, deleting branches or data.
- Security trade-offs, or expanding scope beyond the item being fixed.

Everything else (naming, structure, refactors within scope, test design, choices with a conventional answer) is yours: decide, and record it under "Decisions I made" in the PR with the alternative you rejected. When you must ask, batch all questions in one message, each with your recommendation.

### Branches and PRs
- One item per branch, named `fix/<item-id>-<kebab-summary>` (e.g. `fix/F9-practice-session-expiry`), from the latest `origin/main`.
- Before starting, run `gh pr list` and check whether an open PR touches the same files. If it does, say so in your PR and describe how the conflict should be resolved. Merge `origin/main` into your branch again right before opening the PR.
- Never merge your own PR and never force-push; the owner merges.
- `main` is protected: a PR can only merge when every CI job passes on a branch that's up to date with `main` (GitHub's "Update branch" button, or merge `origin/main` and push). The required checks are the job names in `.github/workflows/ci.yml`; changing a job's name means updating the branch protection in the same PR, or PRs wait on a check that never runs.
- PR body sections: Problem, Fix, Decisions I made, Behaviour/API changes, Interactions with open PRs, Testing, Not tested.

### Attribution
- No `Co-authored-by`, `Claude-Session`, "Generated with Claude Code" or session links in commits or PR bodies.
- PRs are squash-merged with the PR title and description as the commit message (the only merge method enabled), so the description is what reaches `main`. Before opening or editing a PR, run `PR_BODY="<description>" scripts/check-attribution.sh --description`; CI runs the same check whenever a description changes, and the PR can't merge until it passes.
- Keep commit messages clean too: `scripts/check-attribution.sh` checks the commits in `origin/main..HEAD` before you push.

### Definition of done (all of these, every PR)
1. Unit tests: `dotnet test`, `npm test`, `python -m unittest test_roster_validation` (if `tools/` changed); plus `tsc` and `npm run build`.
2. The real API against a throwaway database (restore a backup with `DB_NAME=...`, or migrate an empty one and add minimal rows), in Production mode with `AllowedOrigins` set, covering success and error paths. Drop the database afterwards.
3. Any change a player or admin can see: Playwright through every affected mode (daily, practice, past-challenge replay, both sport families, admin), including slow, failed and empty responses (use `page.route`).
4. Paid APIs are never called: use a fake key (rejected free) or mock the response.
5. Use non-default ports (API 5082, Vite 5175 with `API_PROXY_TARGET`); never stop a process you didn't start.
6. The PR's Testing section lists what ran with results; "Not tested" says what wasn't and why.
7. CI (`.github/workflows/ci.yml`) is green on the PR. It repeats steps 1 and part of 3 on an empty database; it doesn't replace 2 and 3.

### Code standards
- C#: an interface for every DI service; `sealed` classes; records for DTOs and value types; one public type per file; options classes with validation instead of `IConfiguration` lookups; `ActionResult<T>` on every action; services throw `NotFoundException`/`InvalidRequestException`; controllers never catch; pass `CancellationToken` through.
- TypeScript/React: strict types, no `any`; derive state instead of syncing it with effects; side effects in hooks; Vitest tests for new logic.
- Python: type hints, PEP 8, parameterised SQL, context managers.
- Match the surrounding code's style and comment density.

### Self-review before opening the PR
- Read the whole diff top to bottom.
- If you introduce or change a convention, grep for every existing place it applies and update them all in the same PR, or list the ones left out and why.
- Update README/CLAUDE.md when you change an endpoint, a command or a convention.
- If a review of your PR finds an issue that could recur, add the general lesson to this file in a follow-up commit.

## Adding player data

Players are added with the roster research agent (`tools/roster_research_agent.py`, run by the user since it uses their Claude subscription): it researches candidates, checks every value against the sport's columns and existing values, shows each player's difficulty tier (`--tiers`, default leaning Medium), and writes only the players the user accepts, after confirming the target database. Don't write player rows by hand.

Player names are unique per sport (the `(SportId, Name)` index). A different person with the same name as an existing player in that sport gets a disambiguated name: their country as written in the `country` field, e.g. `John Smith (New Zealand)`, or their birth year if they also share a country, e.g. `John Smith (b. 1985)`. The agent's prompt already does this; use the same format when renaming a player in the admin interface.

## Known gotchas

**When you make a genuinely avoidable error during a task, one that would likely recur for a future session working on something different, add a concise line here before finishing, rather than letting the lesson disappear when the session ends.** Not every mistake qualifies, a one-off environment quirk or something already covered below doesn't need a new line. A wrong assumption about how something actually behaves, confirmed wrong only after real back-and-forth, does.

- `Players.IsOverridden` / `Players.DifficultyOverride` let a player's computed difficulty tier be manually overridden (e.g., a player famous for one moment despite modest stats). New players are always written with `IsOverridden = false, DifficultyOverride = NULL` (the roster agent does this); overrides are set through the admin interface.
- Difficulty thresholds differ per tour for Cricket (`CricketRules.ThresholdsByTour`, Men's and Women's differ) but are shared by both Tennis tours (`TennisRules`). Don't assume one formula applies everywhere.
- Attribute key names must match exactly what's in the `AttributeDefinitions` table, verify rather than assume (e.g., it's `grand_slam_titles` and `turned_pro_year`, not `grand_slams` or `turned_pro`).
- `tools/` contains standalone Python scripts (a roster research agent), a different language and set of conventions from the rest of the codebase, don't assume it follows the same patterns as the C#/React app.
- The `claude-agent-sdk` Python package's `ClaudeAgentOptions` has no `allow_dangerously_skip_permissions` parameter (at least as of version 0.2.156), `permission_mode="bypassPermissions"` alone is sufficient for headless tool approval. Verify exact parameters via `inspect.signature()` against the actually installed version rather than trusting external documentation, which may describe a different version or language binding.
- The same SDK's `allowed_tools` is a plain list of tool name strings with no way to attach extra configuration like `allowed_domains`, unlike the raw Messages API's tool definition format, which does support this directly. If a tool needs domain restriction through this SDK, a prompt-level instruction is the fallback, not a technical guarantee.
- `db.Database.Migrate()` in `Program.cs` creates the target database if it doesn't exist instead of failing, so a connection string with a mistyped database name starts the app normally against a new, empty database (schema, no players). When pointing at a database that may not exist, check `pg_database` afterwards and drop anything created by accident.
- A merged PR ignores later pushes to its branch: the commits stay on the branch and never reach `main`. Before pushing a follow-up commit to an open PR, check it's still open (`gh pr view <n> --json state`); if it was merged, open a new PR from `origin/main` with the commit cherry-picked.
