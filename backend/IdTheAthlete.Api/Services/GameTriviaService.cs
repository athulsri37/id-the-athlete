using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Errors;

namespace IdTheAthlete.Api.Services;

// Unlocks a finished game's AI trivia blurb from the trivia token in its
// game-over guess response. Kept apart from GameService so the guess path
// has no dependency on the AI call at all. The token is only issued at game
// over, so this can't reveal anything about a game still in progress.
public sealed class GameTriviaService : IGameTriviaService
{
    private readonly GameDbContext _db;
    private readonly IGameTokenService _gameTokens;
    private readonly IAiTriviaService _aiTriviaService;

    public GameTriviaService(GameDbContext db, IGameTokenService gameTokens, IAiTriviaService aiTriviaService)
    {
        _db = db;
        _gameTokens = gameTokens;
        _aiTriviaService = aiTriviaService;
    }

    // Null whenever trivia is unavailable (disabled, no API key, the AI call
    // failed) -- it's cosmetic, the frontend just shows nothing. The
    // cancellation token covers the lookups only, not the AI call: a blurb
    // that finishes after the player has left is still cached for the next
    // player to finish a game on the same answer. A bad token is an
    // InvalidRequestException (400), an unknown sport a NotFoundException.
    public async Task<string?> GetTriviaAsync(string sportSlug, string triviaToken, CancellationToken cancellationToken = default)
    {
        var grant = _gameTokens.ReadTrivia(triviaToken);

        var sportId = await _db.Sports
            .Where(s => s.Slug == sportSlug)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Sport '{sportSlug}' not found");

        if (grant.SportId != sportId)
            throw new InvalidRequestException("This trivia token is for a different sport.");

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.Id == grant.PlayerId && p.SportId == sportId, cancellationToken)
            // Only if the player was deleted after the game ended.
            ?? throw new InvalidRequestException("This game's player no longer exists.");

        return await _aiTriviaService.GetTriviaBlurbAsync(player);
    }
}
