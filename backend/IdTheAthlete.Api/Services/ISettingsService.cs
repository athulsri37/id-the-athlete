namespace IdTheAthlete.Api.Services;

public interface ISettingsService
{
    Task<bool> IsEnabledAsync(string key);
    Task<Dictionary<string, decimal>> GetDecimalsAsync(IEnumerable<string> keys);
    Task<string> GetThemeAsync();
    Task<List<KeyValuePair<string, string>>> GetAllAsync();
    Task UpdateAsync(string key, string newValue);
}
