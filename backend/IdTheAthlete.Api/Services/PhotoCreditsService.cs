using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;

namespace IdTheAthlete.Api.Services;

// The author and licence of every player photo, for the Photo credits page:
// the game shows Wikimedia Commons photos under their licences, which ask
// for each one to be credited. Registered Scoped (depends on GameDbContext).
public sealed class PhotoCreditsService : IPhotoCreditsService
{
    private readonly GameDbContext _db;

    public PhotoCreditsService(GameDbContext db)
    {
        _db = db;
    }

    public async Task<List<PhotoCreditDto>> GetPhotoCreditsAsync(CancellationToken cancellationToken)
    {
        return await _db.Players
            .Where(p => p.PhotoFile != null)
            .OrderBy(p => p.Sport!.Name)
            .ThenBy(p => p.Name)
            .Select(p => new PhotoCreditDto
            {
                SportSlug = p.Sport!.Slug,
                PlayerName = p.Name,
                File = p.PhotoFile!,
                Credit = p.PhotoCredit ?? "",
                License = p.PhotoLicense ?? ""
            })
            .ToListAsync(cancellationToken);
    }
}
