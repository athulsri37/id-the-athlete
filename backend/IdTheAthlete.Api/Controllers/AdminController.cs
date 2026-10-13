using Microsoft.AspNetCore.Mvc;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Controllers;

// Every action here is only reachable with a valid X-Admin-Key --
// AdminAuthMiddleware rejects any request under /api/admin before it
// reaches this controller, so there's no [Authorize]-style attribute
// needed on individual actions.
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IAdminStatsService _stats;

    public AdminController(IAdminService adminService, IAdminStatsService stats)
    {
        _adminService = adminService;
        _stats = stats;
    }

    // GET /api/admin/stats?sport=tennis-men&from=2026-10-01&to=2026-10-31
    // The Stats tab: anonymous gameplay stats for games started in the
    // range (UTC, inclusive). sport, from and to are all optional.
    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> GetStats([FromQuery] string? sport, [FromQuery] string? from, [FromQuery] string? to)
    {
        return await _stats.GetStatsAsync(sport, from, to);
    }

    [HttpGet("sports")]
    public async Task<ActionResult<List<AdminSportDto>>> GetSports()
    {
        return await _adminService.GetSportsAsync();
    }

    [HttpGet("sports/{sportSlug}/players")]
    public async Task<ActionResult<List<AdminPlayerSummaryDto>>> GetPlayers(string sportSlug)
    {
        return await _adminService.GetPlayersAsync(sportSlug);
    }

    [HttpGet("sports/{sportSlug}/attributes/{attributeKey}/distinct-values")]
    public async Task<ActionResult<List<string>>> GetDistinctValues(string sportSlug, string attributeKey)
    {
        return await _adminService.GetDistinctValuesAsync(sportSlug, attributeKey);
    }

    // POST /api/admin/sports/{sportSlug}/difficulty-preview
    // Body: one object per proposed player, attribute key -> value. Returns
    // their difficulty tiers in the same order. Used by the roster agent.
    [HttpPost("sports/{sportSlug}/difficulty-preview")]
    public async Task<ActionResult<List<string>>> PreviewDifficultyTiers(string sportSlug, [FromBody] List<Dictionary<string, string>> players)
    {
        return await _adminService.PreviewDifficultyTiersAsync(sportSlug, players);
    }

    [HttpGet("players/{playerId}")]
    public async Task<ActionResult<AdminPlayerDetailDto>> GetPlayer(int playerId)
    {
        return await _adminService.GetPlayerAsync(playerId);
    }

    [HttpPut("players/{playerId}")]
    public async Task<IActionResult> UpdatePlayer(int playerId, [FromBody] AdminPlayerUpdateDto request)
    {
        await _adminService.UpdatePlayerAsync(playerId, request);
        return Ok(new { message = "Player updated." });
    }

    [HttpGet("settings")]
    public async Task<ActionResult<List<AdminSettingDto>>> GetSettings()
    {
        return await _adminService.GetSettingsAsync();
    }

    [HttpPut("settings/{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] AdminSettingUpdateDto request)
    {
        await _adminService.UpdateSettingAsync(key, request.Value);
        return Ok(new { message = "Setting updated." });
    }
}
