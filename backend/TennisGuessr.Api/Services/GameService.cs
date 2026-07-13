using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using TennisGuessr.Api.Data;
using TennisGuessr.Api.Dtos;
using TennisGuessr.Api.Models;

namespace TennisGuessr.Api.Services;

public class GameService
{
    private readonly GameDbContext _db;

    // In-memory store for practice-mode sessions (sessionId -> playerId).
    // Fine for a small hobby project; would move to a DB table or Redis
    // if this needed to survive server restarts / scale horizontally.
    private static readonly ConcurrentDictionary<string, int> PracticeSessions = new();

    private const int MaxGuesses = 8;
    private static readonly Random Rng = new();

    public GameService(GameDbContext db)
    {
        _db = db;
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

        var eraGroups = difficulty switch
        {
            "easy" => new[] { "current" },
            "medium" => new[] { "current", "2005_2015" },
            "hard" => new[] { "current", "2005_2015", "legend" },
            _ => throw new ArgumentException("Invalid difficulty. Use easy, medium, or hard.")
        };

        var pool = await _db.Players
            .Where(p => p.SportId == sport.Id && eraGroups.Contains(p.EraGroup))
            .ToListAsync();

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

    public async Task<GuessResponseDto> SubmitGuessAsync(string sportSlug, GuessRequestDto request, int guessNumber)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        int mysteryPlayerId = request.Mode == "daily"
            ? await GetTodaysMysteryPlayerIdAsync(sport.Id)
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
            }
            else
            {
                clue.IsMatch = string.Equals(guessedValue, mysteryValue, StringComparison.OrdinalIgnoreCase);
            }

            return clue;
        }).ToList();

        if (request.Mode != "daily" && gameOver && !string.IsNullOrEmpty(request.SessionId))
        {
            PracticeSessions.TryRemove(request.SessionId, out _);
        }

        return new GuessResponseDto
        {
            GuessedPlayerName = guessedPlayer.Name,
            IsCorrect = isCorrect,
            Clues = clues,
            GameOver = gameOver,
            AnswerName = gameOver ? mysteryPlayer.Name : null
        };
    }

    private int ResolvePracticeSessionPlayerId(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || !PracticeSessions.TryGetValue(sessionId, out var playerId))
            throw new InvalidOperationException("Practice session not found or has expired. Start a new game.");

        return playerId;
    }

    private async Task<int> GetTodaysMysteryPlayerIdAsync(int sportId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _db.DailyPuzzles
            .FirstOrDefaultAsync(d => d.SportId == sportId && d.PuzzleDate == today);

        if (existing != null)
            return existing.PlayerId;

        // No puzzle exists for today yet — pick one, avoiding repeats from the last 14 days.
        var cutoff = today.AddDays(-14);
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

        var chosen = eligiblePlayers[Rng.Next(eligiblePlayers.Count)];

        _db.DailyPuzzles.Add(new DailyPuzzle
        {
            SportId = sportId,
            PlayerId = chosen.Id,
            PuzzleDate = today
        });
        await _db.SaveChangesAsync();

        return chosen.Id;
    }
}
