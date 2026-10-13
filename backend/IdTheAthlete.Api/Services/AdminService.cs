using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Errors;

namespace IdTheAthlete.Api.Services;

// Backs the /api/admin/* endpoints (see AdminController), which are only
// reachable with a valid X-Admin-Key -- enforced by AdminAuthMiddleware,
// not by anything in this class. Deliberately separate from GameService:
// this is curator/editing logic, not gameplay logic, and keeping them
// apart means a bug here can't accidentally change how a real game is
// scored or a mystery player is picked.
//
// Every change it saves is also written to the audit log (AdminAuditEntry)
// with the admin key's name (AdminActor) and the old and new values.
public class AdminService : IAdminService
{
    private readonly GameDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ISettingsService _settings;
    private readonly IDifficultyService _difficulty;
    private readonly AdminActor _actor;

    public const int MaxRecentChanges = 500;

    public AdminService(GameDbContext db, TimeProvider timeProvider, ISettingsService settings, IDifficultyService difficulty, AdminActor actor)
    {
        _db = db;
        _timeProvider = timeProvider;
        _settings = settings;
        _difficulty = difficulty;
        _actor = actor;
    }

    // Difficulty tiers for players that aren't in the database yet (the
    // roster agent's proposals), computed by the same rules the game uses so
    // the agent never needs its own copy of the formulas. Each item maps
    // attribute key to value; keys this sport doesn't have are ignored.
    public async Task<List<string>> PreviewDifficultyTiersAsync(string sportSlug, List<Dictionary<string, string>> players)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new NotFoundException($"Sport '{sportSlug}' not found");

        var definitions = await _db.AttributeDefinitions
            .Where(a => a.SportId == sport.Id)
            .ToDictionaryAsync(a => a.Key);

        return players
            .Select(stats => new Player
            {
                SportId = sport.Id,
                AttributeValues = stats
                    .Where(stat => definitions.ContainsKey(stat.Key))
                    .Select(stat => new PlayerAttributeValue { Value = stat.Value, AttributeDefinition = definitions[stat.Key] })
                    .ToList(),
            })
            .Select(player => _difficulty.ComputeDifficultyTier(player, sport))
            .ToList();
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
            ?? throw new NotFoundException($"Sport '{sportSlug}' not found");

        return await _db.Players
            .Where(p => p.SportId == sport.Id)
            .OrderBy(p => p.Name)
            .Select(p => new AdminPlayerSummaryDto { Id = p.Id, Name = p.Name })
            .ToListAsync();
    }

    public async Task<List<string>> GetDistinctValuesAsync(string sportSlug, string attributeKey)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new NotFoundException($"Sport '{sportSlug}' not found");

        var attributeDef = await _db.AttributeDefinitions
            .FirstOrDefaultAsync(a => a.SportId == sport.Id && a.Key == attributeKey)
            ?? throw new NotFoundException($"Attribute '{attributeKey}' not found for sport '{sportSlug}'");

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
            ?? throw new NotFoundException("Player not found");

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
                .ToList(),
            IsOverridden = player.IsOverridden,
            DifficultyOverride = player.DifficultyOverride
        };
    }

    private static readonly HashSet<string> ValidDifficultyOverrides = new() { "Easy", "Medium", "Hard" };

    // Server-side validation is deliberate defense in depth: the frontend
    // already restricts numeric fields to type="number" inputs and the
    // difficulty dropdown to these exact 3 options, but this must not be
    // the only thing standing between a malformed value and the database,
    // since the frontend check is trivially bypassable.
    public async Task UpdatePlayerAsync(int playerId, AdminPlayerUpdateDto request)
    {
        var player = await _db.Players
            .Include(p => p.AttributeValues)
                .ThenInclude(v => v.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == playerId)
            ?? throw new NotFoundException("Player not found");

        var changes = new List<string>();
        foreach (var (key, value) in request.Attributes)
        {
            var attributeValue = player.AttributeValues.FirstOrDefault(v => v.AttributeDefinition?.Key == key)
                ?? throw new InvalidRequestException($"Attribute '{key}' not found for this player.");

            // Same rule the game uses to read the value back (see NumericAttributeValue).
            if (attributeValue.AttributeDefinition!.Type == AttributeType.Numeric && !NumericAttributeValue.TryParse(value, out _))
                throw new InvalidRequestException($"Attribute '{key}' is numeric; '{value}' is not a valid number.");

            if (attributeValue.Value != value)
                changes.Add(Change(attributeValue.AttributeDefinition.Label, attributeValue.Value, value));
            attributeValue.Value = value;
        }
        var overrideBefore = player.IsOverridden ? player.DifficultyOverride : null;

        // Player-level "last touched" timestamp -- lets consumers like
        // AiTriviaService detect that cached derived content (e.g. a trivia
        // blurb) predates this edit and should be regenerated.
        if (request.Attributes.Count > 0)
            player.LastModifiedAt = _timeProvider.GetUtcNow().UtcDateTime;

        player.IsOverridden = request.IsOverridden;

        if (!player.IsOverridden)
        {
            // A stale override value must never linger once the toggle is
            // off, regardless of what the request body happened to send.
            player.DifficultyOverride = null;
        }
        else
        {
            if (string.IsNullOrEmpty(request.DifficultyOverride) || !ValidDifficultyOverrides.Contains(request.DifficultyOverride))
                throw new InvalidRequestException("DifficultyOverride must be one of \"Easy\", \"Medium\", or \"Hard\" when IsOverridden is true.");

            player.DifficultyOverride = request.DifficultyOverride;
        }

        var overrideAfter = player.IsOverridden ? player.DifficultyOverride : null;
        if (overrideBefore != overrideAfter)
            changes.Add(Change("Difficulty override", overrideBefore ?? "none", overrideAfter ?? "none"));

        // Saving a player with nothing changed isn't a change worth logging.
        if (changes.Count > 0)
        {
            var sportName = await _db.Sports.Where(s => s.Id == player.SportId).Select(s => s.Name).FirstAsync();
            Audit("Player edited", $"{sportName}: {player.Name}", changes);
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<AdminSettingDto>> GetSettingsAsync()
    {
        var settings = await _settings.GetAllAsync();
        return settings.Select(s => new AdminSettingDto { Key = s.Key, Value = s.Value }).ToList();
    }

    // Validation (including the boolean-setting rule) lives in
    // SettingsService.UpdateAsync, which throws NotFoundException for an
    // unknown key and InvalidRequestException for a bad value.
    public async Task UpdateSettingAsync(string key, string newValue)
    {
        var before = await _db.AppSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();

        await _settings.UpdateAsync(key, newValue);

        // Logged once the change is saved; a refused value isn't a change.
        if (before != null && before != newValue)
        {
            Audit("Setting changed", key, new[] { Change(key, before, newValue) });
            await _db.SaveChangesAsync();
        }
    }

    // Newest first, for the admin tool's "Recent changes" tab.
    public async Task<List<AdminAuditEntryDto>> GetRecentChangesAsync(int limit)
    {
        if (limit < 1 || limit > MaxRecentChanges)
            throw new InvalidRequestException($"limit must be between 1 and {MaxRecentChanges}.");

        return await _db.AdminAuditEntries.AsNoTracking()
            .OrderByDescending(e => e.At).ThenByDescending(e => e.Id)
            .Take(limit)
            .Select(e => new AdminAuditEntryDto { At = e.At, Admin = e.AdminName, Action = e.Action, Target = e.Target, Changes = e.Changes })
            .ToListAsync();
    }

    private void Audit(string action, string target, IEnumerable<string> changes)
    {
        _db.AdminAuditEntries.Add(new AdminAuditEntry
        {
            At = _timeProvider.GetUtcNow().UtcDateTime,
            AdminName = _actor.Name,
            Action = action,
            Target = target,
            Changes = string.Join("; ", changes),
        });
    }

    private static string Change(string what, string before, string after) =>
        $"{what}: {Shown(before)} → {Shown(after)}";

    private static string Shown(string value) => value.Length == 0 ? "(empty)" : value;
}
