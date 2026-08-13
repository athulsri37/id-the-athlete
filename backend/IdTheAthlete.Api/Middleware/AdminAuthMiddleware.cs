using System.Security.Cryptography;
using System.Text;

namespace IdTheAthlete.Api.Middleware;

// Protects every /api/admin/* endpoint. Not full user accounts -- a
// request is authorized if its X-Admin-Key header matches ANY value under
// the AdminKeys config section. Those values live only in
// `dotnet user-secrets` (see backend/IdTheAthlete.Api's user-secrets
// store, never committed to the repo), keyed by name (e.g.
// "AdminKeys:Athul") purely for human bookkeeping -- adding another admin
// is just another user-secrets entry, no code or deploy change needed.
public class AdminAuthMiddleware
{
    private readonly RequestDelegate _next;

    public AdminAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (!context.Request.Path.StartsWithSegments("/api/admin"))
        {
            await _next(context);
            return;
        }

        var providedKey = context.Request.Headers["X-Admin-Key"].FirstOrDefault();
        var validKeys = configuration.GetSection("AdminKeys").GetChildren()
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrEmpty(v));

        if (!string.IsNullOrEmpty(providedKey) && validKeys.Any(validKey => FixedTimeEquals(providedKey!, validKey!)))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "Missing or invalid admin key." });
    }

    // A plain == comparison on a secret leaks its length/prefix via timing;
    // this is a small, cheap thing to get right for a header value an
    // attacker could otherwise brute-force character by character.
    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return aBytes.Length == bBytes.Length && CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
