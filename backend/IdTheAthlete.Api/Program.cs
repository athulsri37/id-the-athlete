using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Errors;
using IdTheAthlete.Api.Middleware;
using IdTheAthlete.Api.Rules;
using IdTheAthlete.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Hosts like Render assign the listening port through a PORT environment
// variable and won't expand it inside ASPNETCORE_URLS, so bind to it here.
// An explicit ASPNETCORE_URLS (or --urls) still takes precedence, and
// `dotnet run` is unaffected because its launch profile sets the URL.
var hostPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(hostPort) && string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{hostPort.Trim()}");
}

builder.Services.AddControllers();

// Every API error is a problem response (RFC 9457). ClientErrorException
// subclasses become 4xx responses carrying their message; any other
// exception becomes a 500 that reveals nothing (see ClientErrorExceptionHandler).
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path);
builder.Services.AddExceptionHandler<ClientErrorExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAdminStatsService, AdminStatsService>();
builder.Services.AddScoped<ISportCatalogService, SportCatalogService>();
builder.Services.AddOptions<AnthropicOptions>()
    .Bind(builder.Configuration.GetSection(AnthropicOptions.SectionName));
builder.Services.AddHttpClient<IAiTriviaService, AiTriviaService>((services, client) =>
    client.Timeout = services.GetRequiredService<IOptions<AnthropicOptions>>().Value.RequestTimeout);
builder.Services.AddScoped<IGameTriviaService, GameTriviaService>();
builder.Services.AddSingleton<AiTriviaState>();
builder.Services.AddHostedService<DailyPuzzleGenerationService>();

// GameService's former responsibilities, now split into focused
// components (see each class's own file for why). Most depend on
// GameDbContext or ISettingsService (Scoped); PracticeSessionService needs
// one shared instance for the app's lifetime -> Singleton, holding the
// state itself rather than via a static field.
builder.Services.AddScoped<IDifficultyService, DifficultyService>();

// One ISportRules per sport family (Sport.Family). A new sport adds its
// rules class here; SportRulesRegistry picks the right one per sport.
builder.Services.AddScoped<ISportRules, TennisRules>();
builder.Services.AddScoped<ISportRules, CricketRules>();
builder.Services.AddScoped<ISportRulesRegistry, SportRulesRegistry>();
builder.Services.AddSingleton<IPracticeSessionService, PracticeSessionService>();
builder.Services.AddOptions<PracticeSessionOptions>()
    .Bind(builder.Configuration.GetSection(PracticeSessionOptions.SectionName))
    .Validate(o => o.IdleTimeout > TimeSpan.Zero, $"{PracticeSessionOptions.SectionName}:IdleTimeout must be positive.")
    .ValidateOnStart();
builder.Services.AddScoped<IDailyPuzzleService, DailyPuzzleService>();
builder.Services.AddScoped<IGameStatsService, GameStatsService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddSingleton<SettingsFailureLogThrottle>();
builder.Services.AddOptions<GameTokenOptions>()
    .Bind(builder.Configuration.GetSection(GameTokenOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<GameTokenOptions>, GameTokenOptionsValidator>();
builder.Services.AddSingleton<IGameTokenService, GameTokenService>();

// Comma-separated list of frontend origins allowed to call the API from a
// browser, e.g. "https://<frontend>.onrender.com". Set via the AllowedOrigins
// config key or environment variable; appsettings.Development.json supplies
// the local dev origins. When empty, no cross-origin browser requests are
// allowed (same-origin and server-to-server calls are unaffected).
var allowedOrigins = (builder.Configuration["AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Apply pending migrations on startup. They also create the reference data
// (sports, clue columns, default settings) on an empty database; players are
// added through the roster agent and the admin tool.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.Migrate();

    // Refuse to start if any sport can't be scored (unknown family, or a
    // tour its family's rules don't support), instead of failing mid-game.
    scope.ServiceProvider.GetRequiredService<ISportRulesRegistry>()
        .EnsureEverySportIsSupported(db.Sports.AsNoTracking().ToList());
}
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// First, so it catches exceptions from everything after it.
app.UseExceptionHandler();
app.UseCors();
app.UseMiddleware<AdminAuthMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();