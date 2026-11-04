namespace IdTheAthlete.Api.Dtos;

public class AdminSportDto
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class AdminPlayerSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AdminAttributeValueDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "categorical" | "numeric"
    public string Value { get; set; } = string.Empty;
}

public class AdminPlayerDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<AdminAttributeValueDto> Attributes { get; set; } = new();
    public bool IsOverridden { get; set; }
    public string? DifficultyOverride { get; set; }
    // Null when the player has no photo.
    public AdminPlayerPhotoDto? Photo { get; set; }
}

// A player's photo, a file on Wikimedia Commons (see Player.PhotoFile).
public class AdminPlayerPhotoDto
{
    public string File { get; set; } = string.Empty;
    public string Credit { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public int FocusX { get; set; } = 50;
    public int FocusY { get; set; } = 50;
    public int Zoom { get; set; } = 100; // percent: 100 = the whole photo, up to 300
}

public class AdminPlayerPhotoUpdateDto
{
    public string File { get; set; } = string.Empty;
    public string Credit { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public int FocusX { get; set; } = 50;
    public int FocusY { get; set; } = 50;
    public int Zoom { get; set; } = 100; // percent: 100 = the whole photo, up to 300
    // Omitted for a Control Room edit, "photo-review" for the photo review
    // tool. Any other value is refused.
    public string? Source { get; set; }
}

public class AdminPlayerUpdateDto
{
    // The player's new name; omitted (null) keeps the current one, so tools
    // that only write clue values (the stat refresh) never send it.
    public string? Name { get; set; }
    // AttributeDefinition.Key -> new value
    public Dictionary<string, string> Attributes { get; set; } = new();
    public bool IsOverridden { get; set; }
    // Ignored server-side (forced to null) when IsOverridden is false --
    // see AdminService.UpdatePlayerAsync.
    public string? DifficultyOverride { get; set; }
    // Where the edit comes from, shown as the Changes tab's action: omitted
    // for a Control Room edit, "stat-refresh" for the stat refresh tool.
    // Any other value is refused.
    public string? Source { get; set; }
}

public class AdminSettingDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class AdminSettingUpdateDto
{
    public string Value { get; set; } = string.Empty;
}

// One row of the "Recent changes" tab (see AdminAuditEntry).
public class AdminAuditEntryDto
{
    public DateTime At { get; set; } // UTC
    public string Admin { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Changes { get; set; } = string.Empty;
}
