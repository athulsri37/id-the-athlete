using Microsoft.AspNetCore.Mvc;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Controllers;

[ApiController]
[Route("api/sports/{sportSlug}/game")]
public class GameController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly IDailyPuzzleService _dailyPuzzleService;
    private readonly IGameTriviaService _gameTriviaService;

    public GameController(IGameService gameService, IDailyPuzzleService dailyPuzzleService, IGameTriviaService gameTriviaService)
    {
        _gameService = gameService;
        _dailyPuzzleService = dailyPuzzleService;
        _gameTriviaService = gameTriviaService;
    }

    // POST /api/sports/tennis-men/game/start?difficulty=easy
    // Starts a new practice-mode game and returns a sessionId to use for guesses.
    [HttpPost("start")]
    public async Task<ActionResult<StartGameResponseDto>> StartGame(string sportSlug, [FromQuery] string difficulty = "easy")
    {
        return await _gameService.StartPracticeGameAsync(sportSlug, difficulty);
    }

    // POST /api/sports/tennis-men/game/guess
    // Submits a guess for either the daily puzzle (mode=daily, plus the
    // gameToken from the previous guess after the first one) or an active
    // practice session (mode=easy|medium|hard, sessionId required).
    [HttpPost("guess")]
    public async Task<ActionResult<GuessResponseDto>> SubmitGuess(string sportSlug, [FromBody] GuessRequestDto request)
    {
        return await _gameService.SubmitGuessAsync(sportSlug, request);
    }

    // GET /api/sports/tennis-men/game/trivia?triviaToken=...
    // The finished game's AI trivia blurb, fetched after the result is
    // shown so the final guess never waits on the AI call. triviaToken comes
    // from the game-over guess response; triviaBlurb is null when trivia is
    // unavailable.
    [HttpGet("trivia")]
    public async Task<IActionResult> GetTrivia(string sportSlug, [FromQuery] string triviaToken, CancellationToken cancellationToken)
    {
        try
        {
            var blurb = await _gameTriviaService.GetTriviaAsync(sportSlug, triviaToken, cancellationToken);
            return Ok(new TriviaDto { TriviaBlurb = blurb });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // GET /api/sports/tennis-men/game/hint/country?mode=easy&sessionId=...
    // Free hint that reveals only the mystery player's country — nothing
    // else about them — once 5 guesses have been made. mode/sessionId/
    // gameToken identify the game the same way the guess endpoint does
    // (sessionId for practice, gameToken for mode=daily). date (yyyy-MM-dd)
    // is daily-mode only and optional -- omitted means today, otherwise a
    // specific Past Challenge date.
    [HttpGet("hint/country")]
    public async Task<ActionResult<CountryHintDto>> GetCountryHint(string sportSlug, [FromQuery] string mode = "daily", [FromQuery] string? sessionId = null, [FromQuery] string? date = null, [FromQuery] string? gameToken = null)
    {
        var country = await _gameService.GetCountryHintAsync(sportSlug, mode, sessionId, date, gameToken);
        return new CountryHintDto { Country = country };
    }

    // GET /api/sports/tennis-men/attributes
    // Ordered attribute set (key + label) for this sport -- powers the
    // clue-grid column headers, which must reflect whatever attributes this
    // sport actually has rather than assuming Tennis's fixed set.
    [HttpGet("/api/sports/{sportSlug}/attributes")]
    public async Task<ActionResult<List<AttributeDefinitionDto>>> GetAttributeDefinitions(string sportSlug)
    {
        return await _gameService.GetAttributeDefinitionsAsync(sportSlug);
    }

    // GET /api/sports/tennis-men/daily-puzzles
    // Every date that has an existing Daily Challenge puzzle for this
    // sport, most recent first -- powers the Past Challenges list. No
    // completion status here; that's tracked client-side.
    [HttpGet("/api/sports/{sportSlug}/daily-puzzles")]
    public async Task<ActionResult<List<string>>> GetDailyPuzzleDates(string sportSlug)
    {
        return await _dailyPuzzleService.GetDailyPuzzleDatesAsync(sportSlug);
    }
}
