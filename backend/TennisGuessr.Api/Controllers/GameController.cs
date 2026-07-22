using Microsoft.AspNetCore.Mvc;
using TennisGuessr.Api.Dtos;
using TennisGuessr.Api.Services;

namespace TennisGuessr.Api.Controllers;

[ApiController]
[Route("api/sports/{sportSlug}/game")]
public class GameController : ControllerBase
{
    private readonly GameService _gameService;

    public GameController(GameService gameService)
    {
        _gameService = gameService;
    }

    // POST /api/sports/tennis/game/start?difficulty=easy
    // Starts a new practice-mode game and returns a sessionId to use for guesses.
    [HttpPost("start")]
    public async Task<IActionResult> StartGame(string sportSlug, [FromQuery] string difficulty = "easy")
    {
        try
        {
            var result = await _gameService.StartPracticeGameAsync(sportSlug, difficulty);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST /api/sports/tennis/game/guess?guessNumber=1
    // Submits a guess for either the daily puzzle (mode=daily) or an
    // active practice session (mode=easy|medium|hard, sessionId required).
    [HttpPost("guess")]
    public async Task<IActionResult> SubmitGuess(string sportSlug, [FromBody] GuessRequestDto request, [FromQuery] int guessNumber = 1)
    {
        try
        {
            var result = await _gameService.SubmitGuessAsync(sportSlug, request, guessNumber);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // GET /api/sports/tennis/game/hint/country?mode=easy&sessionId=...
    // Free hint that reveals only the mystery player's country — nothing
    // else about them. mode/sessionId identify the mystery player the same
    // way the guess endpoint does (sessionId is omitted for mode=daily).
    [HttpGet("hint/country")]
    public async Task<IActionResult> GetCountryHint(string sportSlug, [FromQuery] string mode = "daily", [FromQuery] string? sessionId = null)
    {
        try
        {
            var country = await _gameService.GetCountryHintAsync(sportSlug, mode, sessionId);
            return Ok(new CountryHintDto { Country = country });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
