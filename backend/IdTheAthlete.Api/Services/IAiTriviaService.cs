using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

public interface IAiTriviaService
{
    Task<string?> GetTriviaBlurbAsync(Player player);
}
