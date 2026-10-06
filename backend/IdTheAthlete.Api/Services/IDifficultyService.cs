using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

public interface IDifficultyService
{
    string ComputeDifficultyTier(Player player, string sportSlug);
}
