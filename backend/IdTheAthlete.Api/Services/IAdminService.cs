using IdTheAthlete.Api.Dtos;

namespace IdTheAthlete.Api.Services;

public interface IAdminService
{
    Task<List<AdminSportDto>> GetSportsAsync();
    Task<List<AdminPlayerSummaryDto>> GetPlayersAsync(string sportSlug);
    Task<List<string>> GetDistinctValuesAsync(string sportSlug, string attributeKey);
    Task<AdminPlayerDetailDto> GetPlayerAsync(int playerId);
    Task UpdatePlayerAsync(int playerId, AdminPlayerUpdateDto request);
    Task<List<AdminSettingDto>> GetSettingsAsync();
    Task UpdateSettingAsync(string key, string newValue);
}
