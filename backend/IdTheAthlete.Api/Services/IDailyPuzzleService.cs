namespace IdTheAthlete.Api.Services;

public interface IDailyPuzzleService
{
    Task<List<string>> GetDailyPuzzleDatesAsync(string sportSlug);
    Task<ResolvedDailyPuzzle> ResolveDailyPuzzleAsync(int sportId, DateOnly? date);
    Task<bool> EnsureDailyPuzzleAsync(int sportId, DateOnly date);
}
