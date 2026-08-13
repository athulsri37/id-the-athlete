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
}

public class AdminPlayerUpdateDto
{
    // AttributeDefinition.Key -> new value
    public Dictionary<string, string> Attributes { get; set; } = new();
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
