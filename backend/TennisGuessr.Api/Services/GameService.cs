using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using TennisGuessr.Api.Data;
using TennisGuessr.Api.Dtos;
using TennisGuessr.Api.Models;

namespace TennisGuessr.Api.Services;

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

    public GameService(GameDbContext db, AiTriviaService aiTriviaService)
    {
        _db = db;
        _aiTriviaService = aiTriviaService;
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
            .Where(p => tiersAllowed.Contains(ComputeDifficultyTier(p)))
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
    // player whose title count alone would compute too hard). Checked in
    // order — easy, then medium, then hard as the fallback — so the hard
    // branch never needs its own explicit condition: by the time a player
    // falls through both easy (high rank #1 or 20+ titles) and medium
    // (5-19 titles), they're guaranteed to have never reached #1 and have
    // fewer than 5 titles, which already satisfies the stated hard rule.
    private static string ComputeDifficultyTier(Player player)
    {
        if (player.IsOverridden && !string.IsNullOrWhiteSpace(player.DifficultyOverride))
            return player.DifficultyOverride!.ToLowerInvariant();

        var highRank = GetNumericAttribute(player, "career_high_ranking");
        var titles = GetNumericAttribute(player, "career_titles");

        if (highRank == 1 || titles >= 20)
            return "easy";

        if (titles >= 5 && titles < 20)
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
    public async Task<string> GetCountryHintAsync(string sportSlug, string mode, string? sessionId)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        int mysteryPlayerId = mode == "daily"
            ? await GetTodaysMysteryPlayerIdAsync(sport.Id)
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