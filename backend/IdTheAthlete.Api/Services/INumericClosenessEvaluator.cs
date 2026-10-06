namespace IdTheAthlete.Api.Services;

public interface INumericClosenessEvaluator
{
    Task<Dictionary<string, decimal>> LoadCricketSettingsAsync();
    bool IsClose(string attributeKey, decimal guessedNum, decimal mysteryNum, Dictionary<string, decimal> cricketSettings);
}
