namespace IdTheAthlete.Api.Services;

// Bound from the optional "PracticeSessions" configuration section (e.g.
// the PracticeSessions__IdleTimeout environment variable, "01:30:00"), and
// checked at startup in Program.cs.
public sealed class PracticeSessionOptions
{
    public const string SectionName = "PracticeSessions";

    // How long a practice game can go without a guess or hint before it's
    // discarded. Only a long pause mid-game ever reaches it; everything
    // else it removes was abandoned (tab closed, page refreshed, another
    // game started), since the server can't tell when a player leaves.
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(90);
}
