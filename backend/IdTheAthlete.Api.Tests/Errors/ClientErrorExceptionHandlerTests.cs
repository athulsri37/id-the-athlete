using System.Text.Json;
using IdTheAthlete.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdTheAthlete.Api.Tests.Errors;

public class ClientErrorExceptionHandlerTests
{
    private static (ClientErrorExceptionHandler handler, DefaultHttpContext context) Create()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/api/sports/nope/players";
        context.Request.Headers.Accept = "application/json";
        context.Response.Body = new MemoryStream();

        return (new ClientErrorExceptionHandler(services.GetRequiredService<IProblemDetailsService>()), context);
    }

    private static JsonElement ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body).RootElement;
    }

    [Theory]
    [InlineData(typeof(NotFoundException), 404)]
    [InlineData(typeof(InvalidRequestException), 400)]
    public async Task ClientError_WritesProblemResponseWithItsStatusAndMessage(Type exceptionType, int expectedStatus)
    {
        var (handler, context) = Create();
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Sport 'nope' not found")!;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        var body = ReadBody(context);
        Assert.Equal(expectedStatus, body.GetProperty("status").GetInt32());
        Assert.Equal("Sport 'nope' not found", body.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task AnyOtherException_IsLeftForTheDefault500()
    {
        var (handler, context) = Create();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("internal detail"), CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(0, context.Response.Body.Length);
    }
}
