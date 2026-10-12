using Microsoft.AspNetCore.Mvc;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Controllers;

[ApiController]
[Route("api/sports/{sportSlug}/players")]
public class PlayersController : ControllerBase
{
    private readonly IGameService _gameService;

    public PlayersController(IGameService gameService)
    {
        _gameService = gameService;
    }

    // GET /api/sports/tennis-men/players
    // Returns the full player pool for this sport, used to power the
    // guess autocomplete/search box on the frontend.
    [HttpGet]
    public async Task<ActionResult<List<PlayerSummaryDto>>> GetPlayers(string sportSlug)
    {
        return await _gameService.GetPlayerPoolAsync(sportSlug);
    }
}
