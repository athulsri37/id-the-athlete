namespace IdTheAthlete.Api.Rules;

// Decides whether a non-matching clue is "close" (amber), using one snapshot of
// settings for a whole guess. Only called when the two values differ.
public interface IClosenessJudge
{
    bool IsNumericClose(string attributeKey, decimal guessed, decimal mystery);
    bool IsCategoricalClose(string attributeKey, string guessed, string mystery);
}
