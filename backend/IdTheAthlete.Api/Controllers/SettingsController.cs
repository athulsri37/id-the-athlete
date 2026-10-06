using Microsoft.AspNetCore.Mvc;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;

    public SettingsController(ISettingsService settings)
    {
        _settings = settings;
    }

    // GET /api/settings/theme
    [HttpGet("theme")]
    public async Task<IActionResult> GetTheme()
    {
        var theme = await _settings.GetThemeAsync();

        return Ok(new { theme });
    }
}
