namespace IdTheAthlete.Api.Models;

// One change made through the admin tool: who (the admin key's name), when,
// and what changed, written in words so the "Recent changes" tab can show
// it as it is (e.g. "Country: Spain → France"). Kept as text rather than
// structured fields because it's read by people, never by code.
public class AdminAuditEntry
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;  // e.g. "Player edited", "Setting changed"
    public string Target { get; set; } = string.Empty;  // e.g. "Men's Tennis: Roger Federer", "ActiveTheme"
    public string Changes { get; set; } = string.Empty; // e.g. "Country: Spain → France; Difficulty override: none → Hard"
}
