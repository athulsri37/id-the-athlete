namespace IdTheAthlete.Api.Services;

public interface IDailyPuzzleService
{
    Task<List<string>> GetDailyPuzzleDatesAsync(string sportSlug);
    Task<int> ResolveDailyMysteryPlayerIdAsync(int sportId, string? date);
    Task<bool> EnsureDailyPuzzleAsync(int sportId, DateOnly date);
}
