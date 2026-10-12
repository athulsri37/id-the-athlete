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
