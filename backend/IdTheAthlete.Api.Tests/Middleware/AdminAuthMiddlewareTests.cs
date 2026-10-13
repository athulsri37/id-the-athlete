using System.Net;
using System.Text.Json;
using IdTheAthlete.Api.Middleware;
using IdTheAthlete.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Middleware;

// The admin API's door: a right key lets the request through and names the
// admin; wrong keys are counted, and too many lock the address out, right
// key or not.
public class AdminAuthMiddlewareTests
{
    private const string AthulKey = "athul-key-0123456789";
    private const string Client = "203.0.113.7";

    private readonly IConfiguration _config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["AdminKeys:Athul"] = AthulKey, ["AdminKeys:Guest"] = "guest-key-9876543210" })
        .Build();
    private readonly AdminLoginThrottle _throttle = new(new FakeTimeProvider(DateTimeOffset.UnixEpoch));
    private readonly IServiceProvider _services = new ServiceCollection().AddProblemDetails().AddLogging().BuildServiceProvider();

    private sealed record Outcome(int Status, bool ReachedApi, string AdminName, string? RetryAfter, string? Detail);

    private async Task<Outcome> SendAsync(string? key, string path = "/api/admin/sports", string client = Client)
    {
        var reached = false;
        var middleware = new AdminAuthMiddleware(_ => { reached = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(client);
        if (key != null)
            context.Request.Headers["X-Admin-Key"] = key;
        context.Response.Body = new MemoryStream();
        var actor = new AdminActor();

        await middleware.InvokeAsync(context, _config, _services.GetRequiredService<IProblemDetailsService>(), _throttle, actor);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var detail = body.Length == 0 ? null : JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString();
        return new Outcome(context.Response.StatusCode, reached, actor.Name,
            context.Response.Headers.RetryAfter.FirstOrDefault(), detail);
    }

    [Fact]
    public async Task ARightKey_GetsThrough_NamingItsAdmin()
    {
        var outcome = await SendAsync(AthulKey);

        Assert.True(outcome.ReachedApi);
        Assert.Equal("Athul", outcome.AdminName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public async Task AMissingOrWrongKey_Is401(string? key)
    {
        var outcome = await SendAsync(key);

        Assert.Equal((401, false, "Missing or invalid admin key."), (outcome.Status, outcome.ReachedApi, outcome.Detail));
    }

    [Fact]
    public async Task FiveWrongKeys_LockTheAddressOut_EvenForTheRightKey()
    {
        for (var i = 0; i < 5; i++)
            await SendAsync("wrong");

        var outcome = await SendAsync(AthulKey);

        Assert.Equal((429, false), (outcome.Status, outcome.ReachedApi));
        Assert.Equal("900", outcome.RetryAfter);
        Assert.Equal("Too many wrong admin keys. Try again in 15 minutes.", outcome.Detail);
    }

    [Fact]
    public async Task ALockedOutAddress_DoesNotAffectOthers()
    {
        for (var i = 0; i < 5; i++)
            await SendAsync("wrong");

        Assert.True((await SendAsync(AthulKey, client: "198.51.100.9")).ReachedApi);
    }

    [Fact]
    public async Task NonAdminRequests_AreNeverChecked()
    {
        for (var i = 0; i < 5; i++)
            await SendAsync("wrong");

        var outcome = await SendAsync(null, path: "/api/sports");

        Assert.True(outcome.ReachedApi);
        Assert.Equal(200, outcome.Status);
    }
}
