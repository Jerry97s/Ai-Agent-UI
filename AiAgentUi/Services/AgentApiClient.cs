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
    private readonly bool _ownsClient;

    /// <summary>프로덕션: 기본 URL로 <see cref="HttpClient"/>를 소유합니다.</summary>
    public AgentApiClient(string baseUrl)
        : this(CreateOwnedHttpClient(baseUrl), ownsClient: true)
    {
    }

    /// <summary>테스트 또는 커스텀 핸들러 주입 시 사용합니다.</summary>
    public AgentApiClient(HttpClient http, bool ownsClient = false)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsClient = ownsClient;
    }

    private static HttpClient CreateOwnedHttpClient(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var normalized = baseUrl.TrimEnd('/') + "/";
        var http = new HttpClient
        {
            BaseAddress = new Uri(normalized),
            Timeout = TimeSpan.FromMinutes(5),
        };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
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

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }
}
