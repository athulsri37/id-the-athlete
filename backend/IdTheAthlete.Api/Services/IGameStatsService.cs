namespace IdTheAthlete.Api.Services;

public interface IGameStatsService
{
    Task RecordGuessAsync(GameGuessStat guess);
    Task RecordHintAsync(Guid gameId, int guessesSoFar);
}
