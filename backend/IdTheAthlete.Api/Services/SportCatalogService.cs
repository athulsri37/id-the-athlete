using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;

namespace IdTheAthlete.Api.Services;

// The playable tours, for the frontend's home and tour-select screens, so a
// new tour appears there without a frontend change. Registered Scoped
// (depends on GameDbContext).
public sealed class SportCatalogService : ISportCatalogService
{
    private readonly GameDbContext _db;

    public SportCatalogService(GameDbContext db)
    {
        _db = db;
    }

    // Ordered by name so each family's tours keep a stable order
    // ("Men's ..." before "Women's ..."); the frontend groups them by family.
    public async Task<List<SportDto>> GetSportsAsync()
    {
        return await _db.Sports
            .OrderBy(s => s.Name)
            .Select(s => new SportDto
            {
                Slug = s.Slug,
                Name = s.Name,
                ShortName = s.ShortName,
                Family = s.Family,
                UrlName = s.UrlName,
                MaxGuesses = GameRules.MaxGuesses,
                HintAfterGuess = GameRules.HintAfterGuess
            })
            .ToListAsync();
    }
}
