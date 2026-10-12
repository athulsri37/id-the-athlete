using Microsoft.AspNetCore.Diagnostics;

namespace IdTheAthlete.Api.Errors;

// The one place request failures become HTTP responses, so controllers
// never map exceptions by hand. A ClientErrorException becomes a problem
// response (RFC 9457) with its status and message. Anything else is left
// to UseExceptionHandler's default: a logged 500 problem response that
// reveals nothing about the exception.
public sealed class ClientErrorExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ClientErrorException clientError)
            return false;

        httpContext.Response.StatusCode = clientError.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = clientError.StatusCode, Detail = clientError.Message }
        });
    }
}
