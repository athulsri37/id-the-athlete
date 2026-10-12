namespace IdTheAthlete.Api.Errors;

// The resource named in the route doesn't exist (e.g. an unknown sport
// slug or admin player id). A bad reference in the body or query string is
// an InvalidRequestException instead.
public sealed class NotFoundException(string message) : ClientErrorException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}
