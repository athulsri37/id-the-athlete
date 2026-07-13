using Microsoft.AspNetCore.Mvc;
using TennisGuessr.Api.Services;

namespace TennisGuessr.Api.Controllers;

[ApiController]
[Route("api/sports/{sportSlug}/players")]
public class PlayersController : ControllerBase
{
    private readonly GameService _gameService;

    public PlayersController(GameService gameService)
    {
        _gameService = gameService;
    }

    // GET /api/sports/tennis/players
    // Returns the full player pool for this sport, used to power the
    // guess autocomplete/search box on the frontend.
    [HttpGet]
    public async Task<IActionResult> GetPlayers(string sportSlug)
    {
        try
        {
            var players = await _gameService.GetPlayerPoolAsync(sportSlug);
            return Ok(players);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
