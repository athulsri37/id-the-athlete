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
    private readonly AdminService _adminService;

    public AdminController(AdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("sports")]
    public async Task<IActionResult> GetSports()
    {
        return Ok(await _adminService.GetSportsAsync());
    }

    [HttpGet("sports/{sportSlug}/players")]
    public async Task<IActionResult> GetPlayers(string sportSlug)
    {
        try
        {
            return Ok(await _adminService.GetPlayersAsync(sportSlug));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("sports/{sportSlug}/attributes/{attributeKey}/distinct-values")]
    public async Task<IActionResult> GetDistinctValues(string sportSlug, string attributeKey)
    {
        try
        {
            return Ok(await _adminService.GetDistinctValuesAsync(sportSlug, attributeKey));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("players/{playerId}")]
    public async Task<IActionResult> GetPlayer(int playerId)
    {
        try
        {
            return Ok(await _adminService.GetPlayerAsync(playerId));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("players/{playerId}")]
    public async Task<IActionResult> UpdatePlayer(int playerId, [FromBody] AdminPlayerUpdateDto request)
    {
        try
        {
            await _adminService.UpdatePlayerAsync(playerId, request);
            return Ok(new { message = "Player updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        return Ok(await _adminService.GetSettingsAsync());
    }

    [HttpPut("settings/{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] AdminSettingUpdateDto request)
    {
        try
        {
            await _adminService.UpdateSettingAsync(key, request.Value);
            return Ok(new { message = "Setting updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
