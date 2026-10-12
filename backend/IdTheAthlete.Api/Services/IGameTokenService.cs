namespace IdTheAthlete.Api.Services;

public interface IGameTokenService
{
    string Issue(DailyGameState state);
    DailyGameState Read(string token);
    string IssueTrivia(TriviaGrant grant);
    TriviaGrant ReadTrivia(string token);
}
