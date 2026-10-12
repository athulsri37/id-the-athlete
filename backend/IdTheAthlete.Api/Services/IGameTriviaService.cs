namespace IdTheAthlete.Api.Services;

public interface IGameTriviaService
{
    Task<string?> GetTriviaAsync(string sportSlug, string triviaToken, CancellationToken cancellationToken = default);
}
