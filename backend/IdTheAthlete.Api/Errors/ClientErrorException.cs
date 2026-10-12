namespace IdTheAthlete.Api.Errors;

// An expected failure caused by the request, which ClientErrorExceptionHandler
// turns into a 4xx problem response with Message as its detail, so the
// message must be safe and useful for the caller to read. Any other
// exception that escapes a request is treated as a server bug: a 500 with
// no details.
public abstract class ClientErrorException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

// The resource named in the route doesn't exist (e.g. an unknown sport
// slug or admin player id). A bad reference in the body or query string is
// an InvalidRequestException instead.
public sealed class NotFoundException(string message) : ClientErrorException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

// The request can't be carried out as sent: a malformed or unknown value in
// the body or query string, or a game rule it breaks (e.g. guessing after
// the game is over).
public sealed class InvalidRequestException(string message) : ClientErrorException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}
