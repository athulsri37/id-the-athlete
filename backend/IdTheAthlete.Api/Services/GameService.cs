using System.Globalization;
using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Rules;
using IdTheAthlete.Api.Errors;

namespace IdTheAthlete.Api.Services;

// Orchestration only: resolves sports/players, coordinates with the
// focused single-responsibility components below for anything that used
// to be inlined here (difficulty, the sport's closeness rules, practice
// sessions, daily-puzzle selection), and assembles the response DTOs.
// Split out of a single 644-line GameService -- see each component's own
// file for the logic that used to live here.
public class GameService : IGameService
{
    private readonly GameDbContext _db;
    private readonly IDifficultyService _difficultyService;
    private readonly ISportRulesRegistry _sportRules;
    private readonly IPracticeSessionService _practiceSessions;
    private readonly IDailyPuzzleService _dailyPuzzleService;
    private readonly IGameTokenService _gameTokens;
    private readonly IGameStatsService _gameStats;
    private readonly ILogger<GameService> _logger;

    private const int MaxGuesses = GameRules.MaxGuesses;
    private const int HintAfterGuess = GameRules.HintAfterGuess;

    public GameService(
        GameDbContext db,
        IDifficultyService difficultyService,
        ISportRulesRegistry sportRules,
        IPracticeSessionService practiceSessions,
        IDailyPuzzleService dailyPuzzleService,
        IGameTokenService gameTokens,
        IGameStatsService gameStats,
        ILogger<GameService> logger)
    {
        _db = db;
        _difficultyService = difficultyService;
        _sportRules = sportRules;
        _practiceSessions = practiceSessions;
        _dailyPuzzleService = dailyPuzzleService;
        _gameTokens = gameTokens;
        _gameStats = gameStats;
        _logger = logger;
    }

    public async Task<List<PlayerSummaryDto>> GetPlayerPoolAsync(string sportSlug)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new NotFoundException($"Sport '{sportSlug}' not found");

        return await _db.Players
            .Where(p => p.SportId == sport.Id)
            .OrderBy(p => p.Name)
            .Select(p => new PlayerSummaryDto { Id = p.Id, Name = p.Name })
            .ToListAsync();
    }

    public async Task<StartGameResponseDto> StartPracticeGameAsync(string sportSlug, string difficulty)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new NotFoundException($"Sport '{sportSlug}' not found");

        var tiersAllowed = difficulty switch
        {
            "easy" => new[] { "easy" },
            "medium" => new[] { "easy", "medium" },
            "hard" => new[] { "easy", "medium", "hard" },
            _ => throw new InvalidRequestException("Invalid difficulty. Use easy, medium, or hard.")
        };

        var players = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .Where(p => p.SportId == sport.Id)
            .ToListAsync();

        var pool = players
            .Where(p => tiersAllowed.Contains(_difficultyService.ComputeDifficultyTier(p, sport)))
            .ToList();

        if (pool.Count == 0)
            throw new InvalidRequestException("No players available for this difficulty yet.");

        var mysteryPlayer = pool[Random.Shared.Next(pool.Count)];
        var sessionId = _practiceSessions.CreateSession(mysteryPlayer.Id, difficulty);

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
            ?? throw new NotFoundException($"Sport '{sportSlug}' not found");

        bool isDaily = request.Mode == "daily";
        int mysteryPlayerId;
        int guessesSoFar;
        DateOnly dailyPuzzleDate = default;
        Guid gameId;
        string statsMode;

        if (isDaily)
        {
            var dailyGame = await ResolveDailyGameAsync(sport.Id, request.Date, request.GameToken);
            mysteryPlayerId = dailyGame.Puzzle.PlayerId;
            dailyPuzzleDate = dailyGame.Puzzle.PuzzleDate;
            guessesSoFar = dailyGame.GuessesSoFar;
            gameId = dailyGame.GameId;
            statsMode = "daily";
        }
        else
        {
            var practiceGame = _practiceSessions.ResolveSession(request.SessionId);
            mysteryPlayerId = practiceGame.PlayerId;
            guessesSoFar = _practiceSessions.GetGuessCount(request.SessionId);
            gameId = practiceGame.GameId;
            statsMode = practiceGame.Difficulty;
        }

        if (guessesSoFar >= MaxGuesses)
            throw new InvalidRequestException("This game is already over.");

        // Both players in one query (one row when the guess is correct).
        var players = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .Where(p => p.Id == request.PlayerId || p.Id == mysteryPlayerId)
            .ToListAsync();

        var guessedPlayer = players.FirstOrDefault(p => p.Id == request.PlayerId)
            ?? throw new InvalidRequestException("Guessed player not found");

        var mysteryPlayer = players.FirstOrDefault(p => p.Id == mysteryPlayerId)
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

        // The sport's closeness settings are read once per guess (not once
        // per attribute), so a guess costs a fixed number of DB round-trips
        // however many attributes the sport has.
        var closeness = await _sportRules.For(sport).LoadClosenessJudgeAsync();

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
                // A missing or non-numeric value is bad data, not a reason to
                // fail the guess: the clue stays a plain miss (no match, no
                // arrow, not close) and a warning names the value to fix.
                // Both values are checked first so each bad one is logged.
                var guessedIsNumber = TryReadNumber(guessedPlayer, def, guessedValue, out var guessedNum);
                var mysteryIsNumber = TryReadNumber(mysteryPlayer, def, mysteryValue, out var mysteryNum);
                if (!guessedIsNumber || !mysteryIsNumber)
                    return clue;

                clue.IsMatch = guessedNum == mysteryNum;
                clue.Direction = clue.IsMatch ? null : (mysteryNum > guessedNum ? "up" : "down");

                if (!clue.IsMatch)
                {
                    clue.IsClose = closeness.IsNumericClose(def.Key, guessedNum, mysteryNum);
                }
            }
            else
            {
                clue.IsMatch = string.Equals(guessedValue, mysteryValue, StringComparison.OrdinalIgnoreCase);

                if (!clue.IsMatch)
                {
                    clue.IsClose = closeness.IsCategoricalClose(def.Key, guessedValue, mysteryValue);
                }
            }

            return clue;
        }).ToList();

        if (!isDaily && gameOver && !string.IsNullOrEmpty(request.SessionId))
        {
            _practiceSessions.RemoveSession(request.SessionId);
        }

        await _gameStats.RecordGuessAsync(new GameGuessStat(
            gameId, sport.Id, statsMode, isDaily ? dailyPuzzleDate : null,
            mysteryPlayer.Id, guessNumber, gameOver, Won: isCorrect));

        return new GuessResponseDto
        {
            GuessedPlayerId = guessedPlayer.Id,
            GuessedPlayerName = guessedPlayer.Name,
            IsCorrect = isCorrect,
            Clues = clues,
            GameOver = gameOver,
            AnswerName = gameOver ? mysteryPlayer.Name : null,
            // Trivia is fetched separately with this token (see
            // GameTriviaService), so the final guess never waits on the AI call.
            TriviaToken = gameOver ? _gameTokens.IssueTrivia(new TriviaGrant(sport.Id, mysteryPlayer.Id)) : null,
            GameToken = isDaily ? _gameTokens.Issue(new DailyGameState(sport.Id, dailyPuzzleDate, guessNumber, gameId)) : null,
            PuzzleDate = isDaily ? dailyPuzzleDate : null
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
            ?? throw new NotFoundException($"Sport '{sportSlug}' not found");

        int mysteryPlayerId;
        int guessesSoFar;
        Guid gameId;

        if (mode == "daily")
        {
            var dailyGame = await ResolveDailyGameAsync(sport.Id, date, gameToken);
            mysteryPlayerId = dailyGame.Puzzle.PlayerId;
            guessesSoFar = dailyGame.GuessesSoFar;
            gameId = dailyGame.GameId;
        }
        else
        {
            var practiceGame = _practiceSessions.ResolveSession(sessionId);
            mysteryPlayerId = practiceGame.PlayerId;
            guessesSoFar = _practiceSessions.GetGuessCount(sessionId);
            gameId = practiceGame.GameId;
        }

        if (guessesSoFar < HintAfterGuess)
            throw new InvalidRequestException($"The country hint unlocks after {HintAfterGuess} guesses.");

        var mysteryPlayer = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == mysteryPlayerId)
            ?? throw new InvalidOperationException("Mystery player not found");

        var country = mysteryPlayer.AttributeValues
            .FirstOrDefault(v => v.AttributeDefinition?.Key == "country")?.Value;

        if (country == null)
            throw new InvalidOperationException("Country not available for this player.");

        await _gameStats.RecordHintAsync(gameId, guessesSoFar);
        return country;
    }

    private bool TryReadNumber(Player player, AttributeDefinition definition, string value, out decimal number)
    {
        if (NumericAttributeValue.TryParse(value, out number))
            return true;

        _logger.LogWarning(
            "Player {PlayerName} ({PlayerId}) has a non-numeric value '{Value}' for numeric attribute {AttributeKey}; " +
            "showing that clue as a plain miss. Correct it in the admin tool.",
            player.Name, player.Id, value, definition.Key);
        return false;
    }

    // Finds the puzzle a daily game is for and how many guesses it has had.
    // Without a token this is the game's first request: the requested date
    // (null = today) and zero guesses. With one, the token's verified state
    // wins, and it also pins the game to the day it started on, so a game
    // still in progress after midnight UTC keeps its original puzzle.
    private async Task<(ResolvedDailyPuzzle Puzzle, int GuessesSoFar, Guid GameId)> ResolveDailyGameAsync(
        int sportId, string? requestedDate, string? gameToken)
    {
        DateOnly? date = null;
        if (requestedDate != null)
        {
            // The API's documented format, read the same way whatever the
            // server's locale.
            if (!DateOnly.TryParseExact(requestedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                throw new InvalidRequestException($"Invalid date '{requestedDate}'. Use yyyy-MM-dd.");
            date = parsedDate;
        }

        // A first guess starts a new game with a new random stats ID.
        if (string.IsNullOrEmpty(gameToken))
            return (await _dailyPuzzleService.ResolveDailyPuzzleAsync(sportId, date), 0, Guid.NewGuid());

        var state = _gameTokens.Read(gameToken);
        if (state.SportId != sportId || (date != null && date != state.PuzzleDate))
            throw new InvalidRequestException("This game token is for a different puzzle. Start the game again.");

        return (await _dailyPuzzleService.ResolveDailyPuzzleAsync(sportId, state.PuzzleDate), state.GuessCount, state.GameId);
    }

    // Ordered attribute set for this sport, used by the frontend to render
    // the clue-grid column headers before any guess exists (each sport's
    // AttributeDefinitions differ -- Tennis has 8, Cricket has 9 -- so the
    // headers can never be a hardcoded list on the client).
    public async Task<List<AttributeDefinitionDto>> GetAttributeDefinitionsAsync(string sportSlug)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new NotFoundException($"Sport '{sportSlug}' not found");

        return await _db.AttributeDefinitions
            .Where(a => a.SportId == sport.Id)
            .OrderBy(a => a.DisplayOrder)
            .Select(a => new AttributeDefinitionDto { Key = a.Key, Label = a.Label })
            .ToListAsync();
    }
}
