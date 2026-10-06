using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Middleware;
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
builder.Services.AddOpenApi();

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddHttpClient<IAiTriviaService, AiTriviaService>();
builder.Services.AddHostedService<DailyPuzzleGenerationService>();

// GameService's former responsibilities, now split into focused
// components (see each class's own file for why). DifficultyService is
// pure/stateless -> Singleton. The rest depend on GameDbContext (Scoped)
// or, for PracticeSessionService, need one shared instance for the app's
// lifetime -> Singleton there too, holding the state itself rather than
// via a static field.
builder.Services.AddSingleton<IDifficultyService, DifficultyService>();
builder.Services.AddScoped<NumericClosenessEvaluator>();
builder.Services.AddScoped<CategoricalClosenessEvaluator>();
builder.Services.AddSingleton<PracticeSessionService>();
builder.Services.AddScoped<DailyPuzzleService>();

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

// Apply pending migrations on startup. Data seeding is handled separately
// and manually via SeedTool (see backend/SeedTool) — never automatically.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
app.UseMiddleware<AdminAuthMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();