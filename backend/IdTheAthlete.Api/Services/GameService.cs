using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Geo;
using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

public class GameService
{
    private readonly GameDbContext _db;
    private readonly AiTriviaService _aiTriviaService;

    // In-memory store for practice-mode sessions (sessionId -> playerId).
    // Fine for a small hobby project; would move to a DB table or Redis
    // if this needed to survive server restarts / scale horizontally.
    private static readonly ConcurrentDictionary<string, int> PracticeSessions = new();

    private const int MaxGuesses = 8;
    private static readonly Random Rng = new();

    // "Close" tolerance per numeric attribute, applied only when a guess
    // isn't an exact match. Attributes with no entry here never show a
    // close state.
    // Tennis-only: fixed absolute closeness thresholds, untouched by the
    // Cricket closeness feature below (entirely separate code path).
    private static readonly Dictionary<string, decimal> NumericCloseThresholds = new()
    {
        ["grand_slam_titles"] = 2,
        ["career_high_ranking"] = 5,
        ["turned_pro_year"] = 3,
        ["career_titles"] = 5,
    };

    // Cricket-only: percent-of-actual-value closeness, with a floor so a
    // player with a small actual value (e.g. a bowler on 8 wickets) doesn't
    // get an unreasonably tiny closeness window. Unlike Tennis's fixed
    // thresholds above, both numbers are read from AppSettings fresh on
    // every guess (see GetAppSettingsAsync), not hardcoded, so they can be
    // retuned live without a redeploy. debut_year deliberately has no
    // closeness tier, same as before this feature.
    private static readonly Dictionary<string, (string PercentKey, string FloorKey)> CricketNumericClosenessSettingKeys = new()
    {
        ["combined_matches"] = ("CricketMatchesClosenessPercent", "CricketMatchesClosenessFloor"),
        ["combined_runs"] = ("CricketRunsClosenessPercent", "CricketRunsClosenessFloor"),
        ["combined_wickets"] = ("CricketWicketsClosenessPercent", "CricketWicketsClosenessFloor"),
    };

    // Cricket-only: Role closeness via tag-based grouping -- two roles
    // (that aren't an exact match) are close if they share at least one
    // tag. Gated by CricketRoleClosenessEnabled, checked fresh per guess
    // the same way as the country/numeric flags above. Entirely separate
    // from, and never touches, Tennis's categorical comparison (Plays,
    // Backhand, Active Status), which has no closeness tier at all.
    private static readonly Dictionary<string, string[]> RoleTags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Batter"] = new[] { "Batting" },
        ["Wicketkeeper-Batter"] = new[] { "Batting" },
        ["Batting All-rounder"] = new[] { "Batting", "All-Rounder" },
        ["Bowler"] = new[] { "Bowling" },
        ["Bowling All-rounder"] = new[] { "Bowling", "All-Rounder" },
        ["All-rounder"] = new[] { "All-Rounder" },
    };

    // Cricket-only: Bowling Style closeness, grouped by pace vs. spin and
    // ignoring which arm. "Hasn't Bowled" has no entry -- it's isolated,
    // never close to any other value (an exact "Hasn't Bowled" vs. "Hasn't
    // Bowled" pairing is already a match, so it never reaches this check).
    // Gated by CricketBowlingStyleClosenessEnabled.
    private static readonly Dictionary<string, string> BowlingStyleGroup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Right-arm Pace"] = "Pace",
        ["Left-arm Pace"] = "Pace",
        ["Right-arm Spin"] = "Spin",
        ["Left-arm Spin"] = "Spin",
    };

    private static readonly HashSet<string> CricketSportSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "cricket-men-international",
        "cricket-women-international",
    };

    // Cricket-only: country closeness via cricket-specific regional blocs,
    // NOT the land-border geography in CountryProximity.cs (which is
    // wrong for cricket -- e.g. Pakistan and Bangladesh are closely linked
    // cricket nations despite no shared land border, and Australia-New
    // Zealand, cricket's defining rivalry, also shares no border). Always
    // active for Cricket -- deliberately no AppSettings flag, unlike
    // CountryClosenessEnabled/CricketRoleClosenessEnabled/etc., since this
    // is a straightforward correctness fix to core comparison logic for
    // Cricket, not an experimental or tunable feature. A country not
    // listed here has no bloc and never counts as close via this
    // mechanism (an exact match still works normally regardless). Tennis
    // keeps using CountryProximity.IsClose below, completely unchanged --
    // this dictionary and CountryClosenessEnabled are never consulted for
    // Cricket's country clue.
    private static readonly Dictionary<string, string> CricketCountryBloc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["India"] = "Asia",
        ["Pakistan"] = "Asia",
        ["Bangladesh"] = "Asia",
        ["Sri Lanka"] = "Asia",
        ["Afghanistan"] = "Asia",
        ["Nepal"] = "Asia",
        ["Australia"] = "Oceania",
        ["New Zealand"] = "Oceania",
        ["England"] = "British Isles",
        ["Ireland"] = "British Isles",
        ["Scotland"] = "British Isles",
        ["Netherlands"] = "British Isles",
        ["South Africa"] = "Africa",
        ["Namibia"] = "Africa",
        ["Zimbabwe"] = "Africa",
        ["West Indies"] = "Americas",
        ["USA"] = "Americas",
        ["Canada"] = "Americas",
    };

    private readonly ILogger<GameService> _logger;

    public GameService(GameDbContext db, AiTriviaService aiTriviaService, ILogger<GameService> logger)
    {
        _db = db;
        _aiTriviaService = aiTriviaService;
        _logger = logger;
    }

    public async Task<List<PlayerSummaryDto>> GetPlayerPoolAsync(string sportSlug)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        return await _db.Players
            .Where(p => p.SportId == sport.Id)
            .OrderBy(p => p.Name)
            .Select(p => new PlayerSummaryDto { Id = p.Id, Name = p.Name })
            .ToListAsync();
    }

    public async Task<StartGameResponseDto> StartPracticeGameAsync(string sportSlug, string difficulty)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        var tiersAllowed = difficulty switch
        {
            "easy" => new[] { "easy" },
            "medium" => new[] { "easy", "medium" },
            "hard" => new[] { "easy", "medium", "hard" },
            _ => throw new ArgumentException("Invalid difficulty. Use easy, medium, or hard.")
        };

        var players = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .Where(p => p.SportId == sport.Id)
            .ToListAsync();

        var pool = players
            .Where(p => tiersAllowed.Contains(ComputeDifficultyTier(p, sportSlug)))
            .ToList();

        if (pool.Count == 0)
            throw new InvalidOperationException("No players available for this difficulty yet.");

        var mysteryPlayer = pool[Rng.Next(pool.Count)];
        var sessionId = Guid.NewGuid().ToString("N");
        PracticeSessions[sessionId] = mysteryPlayer.Id;

        return new StartGameResponseDto
        {
            Mode = difficulty,
            SessionId = sessionId,
            MaxGuesses = MaxGuesses
        };
    }

    // Computes a player's practice-mode difficulty tier from their stats,
    // unless a curator has explicitly overridden it (e.g. a well-known
    // player whose title count alone would compute too hard). The formula
    // is sport-specific since each sport's AttributeDefinitions differ;
    // sportSlug picks which one applies.
    private static string ComputeDifficultyTier(Player player, string sportSlug)
    {
        if (player.IsOverridden && !string.IsNullOrWhiteSpace(player.DifficultyOverride))
            return player.DifficultyOverride!.ToLowerInvariant();

        // Applied uniformly to both cricket-men-international and
        // cricket-women-international for now -- a Women's-specific
        // formula is a planned future refinement, not implemented yet.
        if (sportSlug is "cricket-men-international" or "cricket-women-international")
            return ComputeCricketDifficultyTier(player);

        return ComputeTennisDifficultyTier(player);
    }

    // Checked in order -- easy, then medium, then hard as the fallback --
    // so the hard branch never needs its own explicit condition: by the
    // time a player falls through both easy (high rank #1 or 20+ titles)
    // and medium (5-19 titles), they're guaranteed to have never reached
    // #1 and have fewer than 5 titles, which already satisfies the stated
    // hard rule.
    private static string ComputeTennisDifficultyTier(Player player)
    {
        var highRank = GetNumericAttribute(player, "career_high_ranking");
        var titles = GetNumericAttribute(player, "career_titles");

        if (highRank == 1 || titles >= 20)
            return "easy";

        if (titles >= 5 && titles < 20)
            return "medium";

        return "hard";
    }

    // Based on combined (all-format) career totals, checked in the same
    // easy/medium/hard-fallback order as Tennis above.
    private static string ComputeCricketDifficultyTier(Player player)
    {
        var runs = GetNumericAttribute(player, "combined_runs");
        var wickets = GetNumericAttribute(player, "combined_wickets");
        var matches = GetNumericAttribute(player, "combined_matches");

        if (runs >= 10000 || wickets >= 300 || matches >= 300)
            return "easy";

        if (runs >= 3000 || wickets >= 100 || matches >= 150)
            return "medium";

        return "hard";
    }

    private static int GetNumericAttribute(Player player, string key)
    {
        var value = player.AttributeValues.FirstOrDefault(v => v.AttributeDefinition?.Key == key)?.Value;
        return value != null && int.TryParse(value, out var parsed) ? parsed : 0;
    }

    public async Task<GuessResponseDto> SubmitGuessAsync(string sportSlug, GuessRequestDto request, int guessNumber)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        int mysteryPlayerId = request.Mode == "daily"
            ? await ResolveDailyMysteryPlayerIdAsync(sport.Id, request.Date)
            : ResolvePracticeSessionPlayerId(request.SessionId);

        var guessedPlayer = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == request.PlayerId)
            ?? throw new InvalidOperationException("Guessed player not found");

        var mysteryPlayer = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == mysteryPlayerId)
            ?? throw new InvalidOperationException("Mystery player not found");

        bool isCorrect = guessedPlayer.Id == mysteryPlayer.Id;
        bool gameOver = isCorrect || guessNumber >= MaxGuesses;

        var attributeDefs = await _db.AttributeDefinitions
            .Where(a => a.SportId == sport.Id)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync();

        var countryClosenessEnabled = await IsCountryClosenessEnabledAsync();
        var cricketRoleClosenessEnabled = await IsAppSettingEnabledAsync("CricketRoleClosenessEnabled");
        var cricketBowlingStyleClosenessEnabled = await IsAppSettingEnabledAsync("CricketBowlingStyleClosenessEnabled");
        var cricketClosenessSettings = await GetAppSettingsAsync(
            CricketNumericClosenessSettingKeys.Values.SelectMany(k => new[] { k.PercentKey, k.FloorKey })
        );

        var clues = attributeDefs.Select(def =>
        {
            var guessedValue = guessedPlayer.AttributeValues.FirstOrDefault(v => v.AttributeDefinitionId == def.Id)?.Value ?? "";
            var mysteryValue = mysteryPlayer.AttributeValues.FirstOrDefault(v => v.AttributeDefinitionId == def.Id)?.Value ?? "";

            var clue = new ClueResultDto
            {
                AttributeKey = def.Key,
                Label = def.Label,
                Type = def.Type.ToString().ToLowerInvariant(),
                Value = guessedValue
            };

            if (def.Type == AttributeType.Numeric)
            {
                var guessedNum = decimal.Parse(guessedValue);
                var mysteryNum = decimal.Parse(mysteryValue);
                clue.IsMatch = guessedNum == mysteryNum;
                clue.Direction = clue.IsMatch ? null : (mysteryNum > guessedNum ? "up" : "down");

                if (!clue.IsMatch)
                {
                    if (CricketNumericClosenessSettingKeys.TryGetValue(def.Key, out var settingKeys) &&
                        cricketClosenessSettings.TryGetValue(settingKeys.PercentKey, out var percent) &&
                        cricketClosenessSettings.TryGetValue(settingKeys.FloorKey, out var floor))
                    {
                        var threshold = Math.Max(mysteryNum * (percent / 100m), floor);
                        clue.IsClose = Math.Abs(mysteryNum - guessedNum) <= threshold;
                    }
                    else if (NumericCloseThresholds.TryGetValue(def.Key, out var tennisThreshold))
                    {
                        clue.IsClose = Math.Abs(mysteryNum - guessedNum) <= tennisThreshold;
                    }
                }
            }
            else
            {
                clue.IsMatch = string.Equals(guessedValue, mysteryValue, StringComparison.OrdinalIgnoreCase);

                if (!clue.IsMatch)
                {
                    if (def.Key == "country" && CricketSportSlugs.Contains(sportSlug))
                    {
                        // Cricket: regional bloc, unconditional -- no flag.
                        clue.IsClose = AreCricketCountriesClose(guessedValue, mysteryValue);
                    }
                    else if (def.Key == "country" && countryClosenessEnabled)
                    {
                        // Tennis (and any other non-Cricket sport): unchanged.
                        clue.IsClose = CountryProximity.IsClose(guessedValue, mysteryValue);
                    }
                    else if (def.Key == "role" && cricketRoleClosenessEnabled)
                    {
                        clue.IsClose = AreCricketRolesClose(guessedValue, mysteryValue);
                    }
                    else if (def.Key == "bowling_style" && cricketBowlingStyleClosenessEnabled)
                    {
                        clue.IsClose = AreCricketBowlingStylesClose(guessedValue, mysteryValue);
                    }
                }
            }

            return clue;
        }).ToList();

        if (request.Mode != "daily" && gameOver && !string.IsNullOrEmpty(request.SessionId))
        {
            PracticeSessions.TryRemove(request.SessionId, out _);
        }

        string? triviaBlurb = gameOver ? await _aiTriviaService.GetTriviaBlurbAsync(mysteryPlayer) : null;

        return new GuessResponseDto
        {
            GuessedPlayerName = guessedPlayer.Name,
            IsCorrect = isCorrect,
            Clues = clues,
            GameOver = gameOver,
            AnswerName = gameOver ? mysteryPlayer.Name : null,
            TriviaBlurb = triviaBlurb
        };
    }

    // Free hint: reveals only the mystery player's country, nothing else.
    // Validation mirrors SubmitGuessAsync's mystery-player resolution:
    // - Practice mode: ResolvePracticeSessionPlayerId throws if the session
    //   is missing, which is also what happens once a practice game ends
    //   (SubmitGuessAsync removes the session on game-over), so an
    //   already-finished practice game is naturally rejected here too.
    // - Daily mode has no server-side per-player session/guess-count state
    //   at all (by design — see GetTodaysMysteryPlayerIdAsync), so there's
    //   nothing server-side to check for "already over" there; the
    //   frontend is responsible for only requesting the hint when it makes
    //   sense, same trust boundary as the client-enforced guess limit.
    public async Task<string> GetCountryHintAsync(string sportSlug, string mode, string? sessionId, string? date = null)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        int mysteryPlayerId = mode == "daily"
            ? await ResolveDailyMysteryPlayerIdAsync(sport.Id, date)
            : ResolvePracticeSessionPlayerId(sessionId);

        var mysteryPlayer = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == mysteryPlayerId)
            ?? throw new InvalidOperationException("Mystery player not found");

        var country = mysteryPlayer.AttributeValues
            .FirstOrDefault(v => v.AttributeDefinition?.Key == "country")?.Value;

        return country ?? throw new InvalidOperationException("Country not available for this player.");
    }

    private int ResolvePracticeSessionPlayerId(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || !PracticeSessions.TryGetValue(sessionId, out var playerId))
            throw new InvalidOperationException("Practice session not found or has expired. Start a new game.");

        return playerId;
    }

    private async Task<bool> IsCountryClosenessEnabledAsync()
    {
        try
        {
            var value = await _db.AppSettings
                .Where(s => s.Key == "CountryClosenessEnabled")
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            return value == "true";
        }
        catch
        {
            return false;
        }
    }

    // General-purpose boolean AppSettings flag, read fresh (no caching)
    // every time it's called. IsCountryClosenessEnabledAsync above predates
    // this and is left as its own method rather than refactored onto this
    // one, to keep this change's diff scoped to what it actually needs.
    private async Task<bool> IsAppSettingEnabledAsync(string key)
    {
        try
        {
            var value = await _db.AppSettings
                .Where(s => s.Key == key)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            return value == "true";
        }
        catch
        {
            return false;
        }
    }

    private static bool AreCricketRolesClose(string guessedRole, string mysteryRole)
    {
        return RoleTags.TryGetValue(guessedRole, out var guessedTags) &&
               RoleTags.TryGetValue(mysteryRole, out var mysteryTags) &&
               guessedTags.Intersect(mysteryTags, StringComparer.OrdinalIgnoreCase).Any();
    }

    private static bool AreCricketCountriesClose(string guessedCountry, string mysteryCountry)
    {
        return CricketCountryBloc.TryGetValue(guessedCountry, out var guessedBloc) &&
               CricketCountryBloc.TryGetValue(mysteryCountry, out var mysteryBloc) &&
               string.Equals(guessedBloc, mysteryBloc, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreCricketBowlingStylesClose(string guessedStyle, string mysteryStyle)
    {
        return BowlingStyleGroup.TryGetValue(guessedStyle, out var guessedGroup) &&
               BowlingStyleGroup.TryGetValue(mysteryStyle, out var mysteryGroup) &&
               string.Equals(guessedGroup, mysteryGroup, StringComparison.OrdinalIgnoreCase);
    }

    // Reads a batch of AppSettings values fresh from the database and
    // parses each as a decimal, skipping any that are missing or
    // unparseable rather than throwing -- callers treat an absent key as
    // "no closeness for this attribute" (see CricketNumericClosenessSettingKeys
    // usage), not an error. Queried in a single round-trip per guess, and
    // never cached, so an operator retuning a threshold via SQL takes
    // effect on the very next guess with no redeploy.
    private async Task<Dictionary<string, decimal>> GetAppSettingsAsync(IEnumerable<string> keys)
    {
        var keyList = keys.ToList();
        var rows = await _db.AppSettings
            .Where(s => keyList.Contains(s.Key))
            .ToListAsync();

        var result = new Dictionary<string, decimal>();
        foreach (var row in rows)
        {
            if (decimal.TryParse(row.Value, out var parsed))
                result[row.Key] = parsed;
        }
        return result;
    }

    // Ordered attribute set for this sport, used by the frontend to render
    // the clue-grid column headers before any guess exists (each sport's
    // AttributeDefinitions differ -- Tennis has 8, Cricket has 9 -- so the
    // headers can never be a hardcoded list on the client).
    public async Task<List<AttributeDefinitionDto>> GetAttributeDefinitionsAsync(string sportSlug)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        return await _db.AttributeDefinitions
            .Where(a => a.SportId == sport.Id)
            .OrderBy(a => a.DisplayOrder)
            .Select(a => new AttributeDefinitionDto { Key = a.Key, Label = a.Label })
            .ToListAsync();
    }

    // Every date (yyyy-MM-dd) that has an existing Daily Challenge puzzle
    // for this sport, most recent first. Deliberately just the dates --
    // completion/streak status is tracked entirely client-side (localStorage),
    // never here.
    public async Task<List<string>> GetDailyPuzzleDatesAsync(string sportSlug)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        var dates = await _db.DailyPuzzles
            .Where(d => d.SportId == sport.Id)
            .OrderByDescending(d => d.PuzzleDate)
            .Select(d => d.PuzzleDate)
            .ToListAsync();

        return dates.Select(d => d.ToString("yyyy-MM-dd")).ToList();
    }

    // date == null means "today" (the existing behavior: creates today's
    // puzzle on first access if it doesn't exist yet). A specific date is
    // always a Past Challenge, which by definition already has a puzzle
    // (the frontend only ever offers dates GetDailyPuzzleDatesAsync
    // returned) -- so this never creates one, only looks it up.
    private async Task<int> ResolveDailyMysteryPlayerIdAsync(int sportId, string? date)
    {
        if (date == null)
            return await GetTodaysMysteryPlayerIdAsync(sportId);

        if (!DateOnly.TryParse(date, out var parsedDate))
            throw new InvalidOperationException($"Invalid date '{date}'.");

        var puzzle = await _db.DailyPuzzles
            .FirstOrDefaultAsync(d => d.SportId == sportId && d.PuzzleDate == parsedDate)
            ?? throw new InvalidOperationException($"No daily puzzle exists for {date}.");

        return puzzle.PlayerId;
    }

    // Lazy/on-demand fallback: only reached when a player's request for
    // today's Daily Challenge arrives before either the scheduled midnight-
    // UTC job (DailyPuzzleGenerationService) or its startup catch-up have
    // generated today's puzzle for this sport. This used to be the ONLY
    // way daily puzzles were generated; it's now purely a safety net, so
    // it logs a warning when it actually creates a puzzle -- that should
    // never happen in normal operation, and means the scheduled mechanism
    // needs investigating.
    private async Task<int> GetTodaysMysteryPlayerIdAsync(int sportId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _db.DailyPuzzles
            .FirstOrDefaultAsync(d => d.SportId == sportId && d.PuzzleDate == today);

        if (existing != null)
            return existing.PlayerId;

        _logger.LogWarning(
            "Lazy-generating today's ({Date}) daily puzzle for sport {SportId} -- neither the scheduled " +
            "midnight-UTC job nor its startup catch-up had created one yet. This is only a safety net; " +
            "if it fires in normal operation, DailyPuzzleGenerationService needs investigating.",
            today, sportId);

        var chosen = await SelectDailyPuzzlePlayerAsync(sportId, today);

        _db.DailyPuzzles.Add(new DailyPuzzle
        {
            SportId = sportId,
            PlayerId = chosen.Id,
            PuzzleDate = today
        });
        await _db.SaveChangesAsync();

        return chosen.Id;
    }

    // Shared by both the lazy fallback above and EnsureDailyPuzzleAsync
    // below, so the "avoid repeats from the last 14 days" rule is defined
    // in exactly one place regardless of which path generates a puzzle.
    private async Task<Player> SelectDailyPuzzlePlayerAsync(int sportId, DateOnly date)
    {
        var cutoff = date.AddDays(-14);
        var recentPlayerIds = await _db.DailyPuzzles
            .Where(d => d.SportId == sportId && d.PuzzleDate > cutoff)
            .Select(d => d.PlayerId)
            .ToListAsync();

        var eligiblePlayers = await _db.Players
            .Where(p => p.SportId == sportId && !recentPlayerIds.Contains(p.Id))
            .ToListAsync();

        // Fall back to the full pool if everything has been used recently
        // (only realistic once the dataset is still small).
        if (eligiblePlayers.Count == 0)
        {
            eligiblePlayers = await _db.Players.Where(p => p.SportId == sportId).ToListAsync();
        }

        return eligiblePlayers[Rng.Next(eligiblePlayers.Count)];
    }

    // Called by DailyPuzzleGenerationService (both its startup catch-up and
    // its scheduled midnight-UTC run) -- the primary, expected path for
    // creating daily puzzles. Idempotent: returns false without touching
    // the database if a puzzle for this sport/date already exists. Also
    // guards against a duplicate-insert race (e.g. the scheduled job and
    // the lazy fallback both reaching the "doesn't exist yet" check at
    // nearly the same moment) by treating the DailyPuzzles unique index
    // violation on (SportId, PuzzleDate) as "someone else just created it"
    // rather than letting the exception propagate.
    public async Task<bool> EnsureDailyPuzzleAsync(int sportId, DateOnly date)
    {
        var alreadyExists = await _db.DailyPuzzles.AnyAsync(d => d.SportId == sportId && d.PuzzleDate == date);
        if (alreadyExists)
            return false;

        var chosen = await SelectDailyPuzzlePlayerAsync(sportId, date);

        _db.DailyPuzzles.Add(new DailyPuzzle
        {
            SportId = sportId,
            PlayerId = chosen.Id,
            PuzzleDate = date
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race to another concurrent generator. If a puzzle now
            // exists for this sport/date (just not the one we picked),
            // that's exactly the outcome we wanted, so treat it as such
            // rather than letting the exception propagate. Any other cause
            // of the same exception type is rare enough here (SQLite/PG
            // unique-violation is by far the likely one) that re-throwing
            // would be the only alternative anyway.
            var nowExists = await _db.DailyPuzzles.AnyAsync(d => d.SportId == sportId && d.PuzzleDate == date);
            if (!nowExists)
                throw;

            return false;
        }

        return true;
    }
}