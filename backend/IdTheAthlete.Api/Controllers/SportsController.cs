using Microsoft.AspNetCore.Mvc;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Controllers;

[ApiController]
[Route("api/sports")]
public class SportsController : ControllerBase
{
    private readonly ISportCatalogService _sports;

    public SportsController(ISportCatalogService sports)
    {
        _sports = sports;
    }

    // GET /api/sports
    // Every playable tour with its family, which the frontend uses to build
    // the home (one button per family) and tour-select screens.
    [HttpGet]
    public async Task<ActionResult<List<SportDto>>> GetSports()
    {
        return await _sports.GetSportsAsync();
    }
}
