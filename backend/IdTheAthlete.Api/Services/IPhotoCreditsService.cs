using IdTheAthlete.Api.Dtos;

namespace IdTheAthlete.Api.Services;

public interface IPhotoCreditsService
{
    Task<List<PhotoCreditDto>> GetPhotoCreditsAsync(CancellationToken cancellationToken);
}
