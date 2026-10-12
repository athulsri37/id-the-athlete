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

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
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
