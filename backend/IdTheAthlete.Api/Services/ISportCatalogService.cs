using IdTheAthlete.Api.Dtos;

namespace IdTheAthlete.Api.Services;

public interface ISportCatalogService
{
    Task<List<SportDto>> GetSportsAsync();
}
