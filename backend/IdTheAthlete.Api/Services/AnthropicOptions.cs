namespace IdTheAthlete.Api.Services;

// Bound from the "Anthropic" configuration section (ApiKey from
// user-secrets locally, the Anthropic__ApiKey environment variable in
// production). Everything here is optional: without an ApiKey, AI trivia
// is simply skipped.
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";
    public const string DefaultModel = "claude-sonnet-5";

    public string? ApiKey { get; set; }
    public string? Model { get; set; }

    // Per attempt, applied to AiTriviaService's HttpClient. HttpClient's
    // default is 100 seconds; a 2-sentence blurb normally takes a few. A
    // timed-out attempt is retried like a network error, so the worst case
    // is AiTriviaService's MaxAttempts timeouts plus backoff.
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
