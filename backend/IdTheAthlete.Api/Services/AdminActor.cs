namespace IdTheAthlete.Api.Services;

// Which admin key made this request: its name under AdminKeys (e.g. "Athul"
// for AdminKeys:Athul), set by AdminAuthMiddleware once the key matches, so
// the audit log can say who changed what. Empty outside admin requests.
// A per-request value holder, not a service, so it has no interface.
// Registered Scoped.
public sealed class AdminActor
{
    public string Name { get; set; } = string.Empty;
}
