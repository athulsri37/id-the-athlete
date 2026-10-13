using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Middleware;

// Protects every /api/admin/* endpoint. Not full user accounts -- a
// request is authorized if its X-Admin-Key header matches ANY value under
// the AdminKeys config section. Those values live only in
// `dotnet user-secrets` (see backend/IdTheAthlete.Api's user-secrets
// store, never committed to the repo), keyed by name (e.g.
// "AdminKeys:Athul") purely for human bookkeeping -- adding another admin
// is just another user-secrets entry, no code or deploy change needed.
//
// The matching key's name is recorded for the request (AdminActor) so the
// audit log says who changed what, and wrong keys are counted per client
// address: too many and the address is locked out for a while
// (AdminLoginThrottle), right key or not.
public sealed class AdminAuthMiddleware
{
    private readonly RequestDelegate _next;

    public AdminAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration, IProblemDetailsService problemDetails,
        IAdminLoginThrottle throttle, AdminActor actor)
    {
        if (!context.Request.Path.StartsWithSegments("/api/admin"))
        {
            await _next(context);
            return;
        }

        var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (throttle.LockedOutFor(client) is { } wait)
        {
            var minutes = (int)Math.Ceiling(wait.TotalMinutes);
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            await RefuseAsync(context, problemDetails, StatusCodes.Status429TooManyRequests,
                $"Too many wrong admin keys. Try again in {minutes} minute{(minutes == 1 ? "" : "s")}.");
            return;
        }

        var providedKey = context.Request.Headers["X-Admin-Key"].FirstOrDefault();
        var matched = string.IsNullOrEmpty(providedKey)
            ? null
            : configuration.GetSection("AdminKeys").GetChildren()
                .FirstOrDefault(c => !string.IsNullOrEmpty(c.Value) && FixedTimeEquals(providedKey, c.Value));

        if (matched != null)
        {
            throttle.RecordSuccess(client);
            actor.Name = matched.Key;
            await _next(context);
            return;
        }

        throttle.RecordFailure(client);
        await RefuseAsync(context, problemDetails, StatusCodes.Status401Unauthorized, "Missing or invalid admin key.");
    }

    // The same problem response format as every other API error.
    private static async Task RefuseAsync(HttpContext context, IProblemDetailsService problemDetails, int status, string detail)
    {
        context.Response.StatusCode = status;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Detail = detail }
        });
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
