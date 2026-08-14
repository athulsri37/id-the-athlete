using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Middleware;
using IdTheAthlete.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddHttpClient<AiTriviaService>();
builder.Services.AddHostedService<DailyPuzzleGenerationService>();

// GameService's former responsibilities, now split into focused
// components (see each class's own file for why). DifficultyService is
// pure/stateless -> Singleton. The rest depend on GameDbContext (Scoped)
// or, for PracticeSessionService, need one shared instance for the app's
// lifetime -> Singleton there too, holding the state itself rather than
// via a static field.
builder.Services.AddSingleton<DifficultyService>();
builder.Services.AddScoped<NumericClosenessEvaluator>();
builder.Services.AddScoped<CategoricalClosenessEvaluator>();
builder.Services.AddSingleton<PracticeSessionService>();
builder.Services.AddScoped<DailyPuzzleService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
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