using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

// Backs the /api/admin/* endpoints (see AdminController), which are only
// reachable with a valid X-Admin-Key -- enforced by AdminAuthMiddleware,
// not by anything in this class. Deliberately separate from GameService:
// this is curator/editing logic, not gameplay logic, and keeping them
// apart means a bug here can't accidentally change how a real game is
// scored or a mystery player is picked.
public class AdminService
{
    private readonly GameDbContext _db;

    public AdminService(GameDbContext db)
    {
        _db = db;
    }

    public async Task<List<AdminSportDto>> GetSportsAsync()
    {
        return await _db.Sports
            .OrderBy(s => s.Name)
            .Select(s => new AdminSportDto { Slug = s.Slug, Name = s.Name })
            .ToListAsync();
    }

    public async Task<List<AdminPlayerSummaryDto>> GetPlayersAsync(string sportSlug)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        return await _db.Players
            .Where(p => p.SportId == sport.Id)
            .OrderBy(p => p.Name)
            .Select(p => new AdminPlayerSummaryDto { Id = p.Id, Name = p.Name })
            .ToListAsync();
    }

    public async Task<List<string>> GetDistinctValuesAsync(string sportSlug, string attributeKey)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        var attributeDef = await _db.AttributeDefinitions
            .FirstOrDefaultAsync(a => a.SportId == sport.Id && a.Key == attributeKey)
            ?? throw new InvalidOperationException($"Attribute '{attributeKey}' not found for sport '{sportSlug}'");

        return await _db.PlayerAttributeValues
            .Where(v => v.AttributeDefinitionId == attributeDef.Id)
            .Select(v => v.Value)
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync();
    }

    public async Task<AdminPlayerDetailDto> GetPlayerAsync(int playerId)
    {
        var player = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == playerId)
            ?? throw new InvalidOperationException("Player not found");

        return new AdminPlayerDetailDto
        {
            Id = player.Id,
            Name = player.Name,
            Attributes = player.AttributeValues
                .OrderBy(v => v.AttributeDefinition!.DisplayOrder)
                .Select(v => new AdminAttributeValueDto
                {
                    Key = v.AttributeDefinition!.Key,
                    Label = v.AttributeDefinition.Label,
                    Type = v.AttributeDefinition.Type.ToString().ToLowerInvariant(),
                    Value = v.Value
                })
                .ToList()
        };
    }

    // Server-side validation is deliberate defense in depth: the frontend
    // already restricts numeric fields to type="number" inputs, but this
    // must not be the only thing standing between a malformed value and
    // the database, since the frontend check is trivially bypassable.
    public async Task UpdatePlayerAsync(int playerId, Dictionary<string, string> updates)
    {
        var player = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == playerId)
            ?? throw new InvalidOperationException("Player not found");

        foreach (var (key, value) in updates)
        {
            var attributeValue = player.AttributeValues.FirstOrDefault(v => v.AttributeDefinition?.Key == key)
                ?? throw new InvalidOperationException($"Attribute '{key}' not found for this player.");

            if (attributeValue.AttributeDefinition!.Type == AttributeType.Numeric && !decimal.TryParse(value, out _))
                throw new InvalidOperationException($"Attribute '{key}' is numeric; '{value}' is not a valid number.");

            attributeValue.Value = value;
            // Marks this specific value as curator-owned so it survives
            // every future SeedTool run regardless of which batch file
            // re-seeds this player -- see PlayerAttributeValue.IsManuallyEdited.
            attributeValue.IsManuallyEdited = true;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<AdminSettingDto>> GetSettingsAsync()
    {
        return await _db.AppSettings
            .OrderBy(s => s.Key)
            .Select(s => new AdminSettingDto { Key = s.Key, Value = s.Value })
            .ToListAsync();
    }

    // Same defense-in-depth rationale as UpdatePlayerAsync: the frontend
    // renders a True/False dropdown for a setting whose current value is
    // exactly "true"/"false", but that inference has to be re-validated
    // here too, since nothing prevents a raw PUT request bypassing it.
    public async Task UpdateSettingAsync(string key, string newValue)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key)
            ?? throw new InvalidOperationException($"Setting '{key}' not found.");

        var wasBoolean = setting.Value == "true" || setting.Value == "false";
        if (wasBoolean && newValue != "true" && newValue != "false")
            throw new InvalidOperationException($"Setting '{key}' is boolean; value must be 'true' or 'false'.");

        setting.Value = newValue;
        await _db.SaveChangesAsync();
    }
}
