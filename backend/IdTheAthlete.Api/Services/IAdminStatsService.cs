using IdTheAthlete.Api.Dtos;

namespace IdTheAthlete.Api.Services;

public interface IAdminStatsService
{
    Task<AdminStatsDto> GetStatsAsync(string? sportSlug, string? from, string? to);
}
