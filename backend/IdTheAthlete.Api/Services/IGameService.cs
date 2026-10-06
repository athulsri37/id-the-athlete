using IdTheAthlete.Api.Dtos;

namespace IdTheAthlete.Api.Services;

public interface IGameService
{
    Task<List<PlayerSummaryDto>> GetPlayerPoolAsync(string sportSlug);
    Task<StartGameResponseDto> StartPracticeGameAsync(string sportSlug, string difficulty);
    Task<GuessResponseDto> SubmitGuessAsync(string sportSlug, GuessRequestDto request, int guessNumber);
    Task<string> GetCountryHintAsync(string sportSlug, string mode, string? sessionId, string? date = null);
    Task<List<AttributeDefinitionDto>> GetAttributeDefinitionsAsync(string sportSlug);
}
