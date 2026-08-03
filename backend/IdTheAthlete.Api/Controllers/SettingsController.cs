using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;

namespace IdTheAthlete.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly GameDbContext _db;

    public SettingsController(GameDbContext db)
    {
        _db = db;
    }

    // GET /api/settings/theme
    [HttpGet("theme")]
    public async Task<IActionResult> GetTheme()
    {
        var theme = await _db.AppSettings
            .Where(s => s.Key == "ActiveTheme")
            .Select(s => s.Value)
            .FirstOrDefaultAsync() ?? "retro";

        return Ok(new { theme });
    }
}