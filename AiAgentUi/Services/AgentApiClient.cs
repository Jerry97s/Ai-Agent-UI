using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiAgentUi.Models;

namespace AiAgentUi.Services;

public sealed class AgentApiClient : IAgentClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly JsonSerializerOptions JsonRead = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public AgentApiClient(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var normalized = baseUrl.TrimEnd('/') + "/";
        _http = new HttpClient
        {
            BaseAddress = new Uri(normalized),
            Timeout = TimeSpan.FromMinutes(5),
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<bool> HealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("health", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var body = new ChatRequestModel { Message = message };
        var json = JsonSerializer.Serialize(body, JsonWrite);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("chat", content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var dto = await JsonSerializer.DeserializeAsync<ChatResponseModel>(stream, JsonRead, cancellationToken)
            .ConfigureAwait(false);
        return dto?.Reply ?? "";
    }

    public void Dispose() => _http.Dispose();
}
