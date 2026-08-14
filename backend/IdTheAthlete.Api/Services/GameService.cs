using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

// Orchestration only: resolves sports/players, coordinates with the
// focused single-responsibility components below for anything that used
// to be inlined here (difficulty, numeric/categorical closeness, practice
// sessions, daily-puzzle selection), and assembles the response DTOs.
// Split out of a single 644-line GameService -- see each component's own
// file for the logic that used to live here.
public class GameService
{
    private readonly GameDbContext _db;
    private readonly AiTriviaService _aiTriviaService;
    private readonly DifficultyService _difficultyService;
    private readonly NumericClosenessEvaluator _numericCloseness;
    private readonly CategoricalClosenessEvaluator _categoricalCloseness;
    private readonly PracticeSessionService _practiceSessions;
    private readonly DailyPuzzleService _dailyPuzzleService;

    private const int MaxGuesses = 8;
    private static readonly Random Rng = new();

    public GameService(
        GameDbContext db,
        AiTriviaService aiTriviaService,
        DifficultyService difficultyService,
        NumericClosenessEvaluator numericCloseness,
        CategoricalClosenessEvaluator categoricalCloseness,
        PracticeSessionService practiceSessions,
        DailyPuzzleService dailyPuzzleService)
    {
        _db = db;
        _aiTriviaService = aiTriviaService;
        _difficultyService = difficultyService;
        _numericCloseness = numericCloseness;
        _categoricalCloseness = categoricalCloseness;
        _practiceSessions = practiceSessions;
        _dailyPuzzleService = dailyPuzzleService;
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
            .Where(p => tiersAllowed.Contains(_difficultyService.ComputeDifficultyTier(p, sportSlug)))
            .ToList();

        if (pool.Count == 0)
            throw new InvalidOperationException("No players available for this difficulty yet.");

        var mysteryPlayer = pool[Rng.Next(pool.Count)];
        var sessionId = _practiceSessions.CreateSession(mysteryPlayer.Id);

        return new StartGameResponseDto
        {
            Mode = difficulty,
            SessionId = sessionId,
            MaxGuesses = MaxGuesses
        };
    }

    public async Task<GuessResponseDto> SubmitGuessAsync(string sportSlug, GuessRequestDto request, int guessNumber)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        int mysteryPlayerId = request.Mode == "daily"
            ? await _dailyPuzzleService.ResolveDailyMysteryPlayerIdAsync(sport.Id, request.Date)
            : _practiceSessions.ResolveSessionPlayerId(request.SessionId);

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

        // Fetched once per guess (not once per attribute) so closeness
        // evaluation for all of a sport's attributes costs a fixed number
        // of DB round-trips regardless of how many attributes it has.
        var categoricalFlags = await _categoricalCloseness.LoadFlagsAsync();
        var cricketNumericSettings = await _numericCloseness.LoadCricketSettingsAsync();

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
                    clue.IsClose = _numericCloseness.IsClose(def.Key, guessedNum, mysteryNum, cricketNumericSettings);
                }
            }
            else
            {
                clue.IsMatch = string.Equals(guessedValue, mysteryValue, StringComparison.OrdinalIgnoreCase);

                if (!clue.IsMatch)
                {
                    clue.IsClose = _categoricalCloseness.IsClose(def.Key, sportSlug, guessedValue, mysteryValue, categoricalFlags);
                }
            }

            return clue;
        }).ToList();

        if (request.Mode != "daily" && gameOver && !string.IsNullOrEmpty(request.SessionId))
        {
            _practiceSessions.RemoveSession(request.SessionId);
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
    // - Practice mode: PracticeSessionService throws if the session is
    //   missing, which is also what happens once a practice game ends
    //   (SubmitGuessAsync removes the session on game-over), so an
    //   already-finished practice game is naturally rejected here too.
    // - Daily mode has no server-side per-player session/guess-count state
    //   at all (by design), so there's nothing server-side to check for
    //   "already over" there; the frontend is responsible for only
    //   requesting the hint when it makes sense, same trust boundary as
    //   the client-enforced guess limit.
    public async Task<string> GetCountryHintAsync(string sportSlug, string mode, string? sessionId, string? date = null)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        int mysteryPlayerId = mode == "daily"
            ? await _dailyPuzzleService.ResolveDailyMysteryPlayerIdAsync(sport.Id, date)
            : _practiceSessions.ResolveSessionPlayerId(sessionId);

        var mysteryPlayer = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == mysteryPlayerId)
            ?? throw new InvalidOperationException("Mystery player not found");

        var country = mysteryPlayer.AttributeValues
            .FirstOrDefault(v => v.AttributeDefinition?.Key == "country")?.Value;

        return country ?? throw new InvalidOperationException("Country not available for this player.");
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
}
