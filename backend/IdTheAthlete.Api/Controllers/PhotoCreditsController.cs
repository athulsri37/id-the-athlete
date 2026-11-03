using Microsoft.AspNetCore.Mvc;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Controllers;

[ApiController]
[Route("api/photo-credits")]
public class PhotoCreditsController : ControllerBase
{
    private readonly IPhotoCreditsService _photoCredits;

    public PhotoCreditsController(IPhotoCreditsService photoCredits)
    {
        _photoCredits = photoCredits;
    }

    // GET /api/photo-credits
    // Every player photo's author and licence, by tour then player, for the
    // Photo credits page.
    [HttpGet]
    public async Task<ActionResult<List<PhotoCreditDto>>> GetPhotoCredits(CancellationToken cancellationToken)
    {
        return await _photoCredits.GetPhotoCreditsAsync(cancellationToken);
    }
}
