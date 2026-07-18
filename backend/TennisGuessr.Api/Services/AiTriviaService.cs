using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TennisGuessr.Api.Data;
using TennisGuessr.Api.Models;

namespace TennisGuessr.Api.Services;

// Generates a short AI trivia blurb about the mystery player once a game ends.
// Purely cosmetic — any failure (missing key, network error, bad response)
// must fall back to null rather than break the core game.
public class AiTriviaService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const string DefaultModel = "claude-sonnet-5";

    private static readonly ConcurrentDictionary<int, string?> Cache = new();

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly GameDbContext _db;

    public AiTriviaService(HttpClient httpClient, IConfiguration configuration, GameDbContext db)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _db = db;
    }

    public async Task<string?> GetTriviaBlurbAsync(Player player)
    {
        // Checked ahead of the cache lookup (not just the API key check) so a
        // disabled flag never gets baked into a cached null — otherwise
        // re-enabling later would leave every already-seen player stuck null.
        if (!await IsEnabledAsync())
            return null;

        if (Cache.TryGetValue(player.Id, out var cached))
            return cached;

        var blurb = await GenerateBlurbAsync(player);
        Cache[player.Id] = blurb;
        return blurb;
    }

    private async Task<bool> IsEnabledAsync()
    {
        try
        {
            var value = await _db.AppSettings
                .Where(s => s.Key == "AiTriviaEnabled")
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            return value == "true";
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> GenerateBlurbAsync(Player player)
    {
        try
        {
            var apiKey = _configuration["Anthropic:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            var model = _configuration["Anthropic:Model"];
            if (string.IsNullOrWhiteSpace(model))
                model = DefaultModel;

            var stats = string.Join(", ", player.AttributeValues
                .Where(v => v.AttributeDefinition != null)
                .Select(v => $"{v.AttributeDefinition!.Label}: {v.Value}"));

            var prompt = $"Write a short, engaging trivia blurb (exactly 2 sentences) about the tennis player {player.Name}. " +
                         $"Use these stats as context: {stats}. Return only the blurb text, with no preamble or quotation marks.";

            var requestBody = new
            {
                model,
                max_tokens = 150,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AnthropicApiUrl)
            {
                Content = JsonContent.Create(requestBody)
            };
            httpRequest.Headers.Add("x-api-key", apiKey);
            httpRequest.Headers.Add("anthropic-version", AnthropicVersion);

            using var response = await _httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<AnthropicMessageResponse>();
            var text = payload?.Content?.FirstOrDefault()?.Text?.Trim();

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private class AnthropicMessageResponse
    {
        [JsonPropertyName("content")]
        public List<AnthropicContentBlock>? Content { get; set; }
    }

    private class AnthropicContentBlock
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}