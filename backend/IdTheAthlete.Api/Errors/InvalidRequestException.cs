namespace IdTheAthlete.Api.Errors;

// The request can't be carried out as sent: a malformed or unknown value in
// the body or query string, or a game rule it breaks (e.g. guessing after
// the game is over).
public sealed class InvalidRequestException(string message) : ClientErrorException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}
