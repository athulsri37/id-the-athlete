namespace IdTheAthlete.Api.Services;

public interface ICategoricalClosenessEvaluator
{
    Task<CategoricalClosenessFlags> LoadFlagsAsync();
    bool IsClose(string attributeKey, string sportSlug, string guessedValue, string mysteryValue, CategoricalClosenessFlags flags);
}
