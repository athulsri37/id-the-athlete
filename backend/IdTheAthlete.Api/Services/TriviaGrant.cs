namespace IdTheAthlete.Api.Services;

// What a trivia token proves: the game for this player just ended, so the
// player may now see their trivia. Issued only in a game-over response,
// where the answer is already revealed, so naming the player here gives
// nothing away. Signed but not encrypted, like DailyGameState.
public sealed record TriviaGrant(int SportId, int PlayerId);
