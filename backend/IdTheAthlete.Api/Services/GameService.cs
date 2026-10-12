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
public class GameService : IGameService
{
    private readonly GameDbContext _db;
    private readonly IAiTriviaService _aiTriviaService;
    private readonly IDifficultyService _difficultyService;
    private readonly INumericClosenessEvaluator _numericCloseness;
    private readonly ICategoricalClosenessEvaluator _categoricalCloseness;
    private readonly IPracticeSessionService _practiceSessions;
    private readonly IDailyPuzzleService _dailyPuzzleService;
    private readonly IGameTokenService _gameTokens;

    private const int MaxGuesses = 8;
    private const int HintAfterGuess = 5;

    public GameService(
        GameDbContext db,
        IAiTriviaService aiTriviaService,
        IDifficultyService difficultyService,
        INumericClosenessEvaluator numericCloseness,
        ICategoricalClosenessEvaluator categoricalCloseness,
        IPracticeSessionService practiceSessions,
        IDailyPuzzleService dailyPuzzleService,
        IGameTokenService gameTokens)
    {
        _db = db;
        _aiTriviaService = aiTriviaService;
        _difficultyService = difficultyService;
        _numericCloseness = numericCloseness;
        _categoricalCloseness = categoricalCloseness;
        _practiceSessions = practiceSessions;
        _dailyPuzzleService = dailyPuzzleService;
        _gameTokens = gameTokens;
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

        var mysteryPlayer = pool[Random.Shared.Next(pool.Count)];
        var sessionId = _practiceSessions.CreateSession(mysteryPlayer.Id);

        return new StartGameResponseDto
        {
            Mode = difficulty,
            SessionId = sessionId,
            MaxGuesses = MaxGuesses
        };
    }

    // The guess number always comes from server-side state -- the practice
    // session, or the signed token from the previous daily guess -- never
    // from the client, so the answer is only revealed after MaxGuesses real
    // guesses (or a correct one).
    public async Task<GuessResponseDto> SubmitGuessAsync(string sportSlug, GuessRequestDto request)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        bool isDaily = request.Mode == "daily";
        int mysteryPlayerId;
        int guessesSoFar;
        DateOnly dailyPuzzleDate = default;

        if (isDaily)
        {
            var dailyGame = await ResolveDailyGameAsync(sport.Id, request.Date, request.GameToken);
            mysteryPlayerId = dailyGame.Puzzle.PlayerId;
            dailyPuzzleDate = dailyGame.Puzzle.PuzzleDate;
            guessesSoFar = dailyGame.GuessesSoFar;
        }
        else
        {
            mysteryPlayerId = _practiceSessions.ResolveSessionPlayerId(request.SessionId);
            guessesSoFar = _practiceSessions.GetGuessCount(request.SessionId);
        }

        if (guessesSoFar >= MaxGuesses)
            throw new InvalidOperationException("This game is already over.");

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

        // Counted only once the guess is known to be valid, so a bad request
        // doesn't use up one of the player's guesses.
        int guessNumber = isDaily ? guessesSoFar + 1 : _practiceSessions.RecordGuess(request.SessionId);

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

        if (!isDaily && gameOver && !string.IsNullOrEmpty(request.SessionId))
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
            TriviaBlurb = triviaBlurb,
            GameToken = isDaily ? _gameTokens.Issue(new DailyGameState(sport.Id, dailyPuzzleDate, guessNumber)) : null
        };
    }

    // Free hint: reveals only the mystery player's country, nothing else,
    // and only once HintAfterGuess guesses have been made. The count comes
    // from the same server-side state as SubmitGuessAsync:
    // - Practice mode: the session's count. A finished practice game's
    //   session has been removed, so it's rejected here too.
    // - Daily mode: the signed token from the latest guess. No token means
    //   no guesses yet, which is always too early.
    public async Task<string> GetCountryHintAsync(string sportSlug, string mode, string? sessionId, string? date = null, string? gameToken = null)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        int mysteryPlayerId;
        int guessesSoFar;

        if (mode == "daily")
        {
            var dailyGame = await ResolveDailyGameAsync(sport.Id, date, gameToken);
            mysteryPlayerId = dailyGame.Puzzle.PlayerId;
            guessesSoFar = dailyGame.GuessesSoFar;
        }
        else
        {
            mysteryPlayerId = _practiceSessions.ResolveSessionPlayerId(sessionId);
            guessesSoFar = _practiceSessions.GetGuessCount(sessionId);
        }

        if (guessesSoFar < HintAfterGuess)
            throw new InvalidOperationException($"The country hint unlocks after {HintAfterGuess} guesses.");

        var mysteryPlayer = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == mysteryPlayerId)
            ?? throw new InvalidOperationException("Mystery player not found");

        var country = mysteryPlayer.AttributeValues
            .FirstOrDefault(v => v.AttributeDefinition?.Key == "country")?.Value;

        return country ?? throw new InvalidOperationException("Country not available for this player.");
    }

    // Finds the puzzle a daily game is for and how many guesses it has had.
    // Without a token this is the game's first request: the requested date
    // (null = today) and zero guesses. With one, the token's verified state
    // wins, and it also pins the game to the day it started on, so a game
    // still in progress after midnight UTC keeps its original puzzle.
    private async Task<(ResolvedDailyPuzzle Puzzle, int GuessesSoFar)> ResolveDailyGameAsync(
        int sportId, string? requestedDate, string? gameToken)
    {
        DateOnly? date = null;
        if (requestedDate != null)
        {
            if (!DateOnly.TryParse(requestedDate, out var parsedDate))
                throw new InvalidOperationException($"Invalid date '{requestedDate}'.");
            date = parsedDate;
        }

        if (string.IsNullOrEmpty(gameToken))
            return (await _dailyPuzzleService.ResolveDailyPuzzleAsync(sportId, date), 0);

        var state = _gameTokens.Read(gameToken);
        if (state.SportId != sportId || (date != null && date != state.PuzzleDate))
            throw new InvalidOperationException("This game token is for a different puzzle. Start the game again.");

        return (await _dailyPuzzleService.ResolveDailyPuzzleAsync(sportId, state.PuzzleDate), state.GuessCount);
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
