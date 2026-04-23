using System.Net;
using System.Text;
using AiAgentUi.Services;
using Xunit;

namespace AiAgentUi.Tests;

public sealed class AgentApiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> SendAsyncImpl { get; set; } =
            (_, _) => throw new InvalidOperationException("Not configured");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => SendAsyncImpl(request, cancellationToken);
    }

    [Fact]
    public async Task SendMessageAsync_posts_chat_and_returns_reply()
    {
        using var stub = new StubHandler();
        stub.SendAsyncImpl = async (req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.EndsWith("/chat", req.RequestUri?.AbsolutePath, StringComparison.Ordinal);
            var body = await req.Content!.ReadAsStringAsync();
            Assert.Contains("hello", body, StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"reply":"world"}""", Encoding.UTF8, "application/json"),
            };
        };

        using var http = new HttpClient(stub) { BaseAddress = new Uri("http://127.0.0.1:8787/") };
        using var client = new AgentApiClient(http);
        var reply = await client.SendMessageAsync("hello");
        Assert.Equal("world", reply);
    }

    [Fact]
    public async Task HealthAsync_returns_true_when_http_200()
    {
        using var stub = new StubHandler();
        stub.SendAsyncImpl = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        using var http = new HttpClient(stub) { BaseAddress = new Uri("http://127.0.0.1:8787/") };
        using var client = new AgentApiClient(http);
        Assert.True(await client.HealthAsync());
    }

    [Fact]
    public async Task HealthAsync_returns_false_on_network_failure()
    {
        using var stub = new StubHandler();
        stub.SendAsyncImpl = (_, _) => throw new HttpRequestException("boom");

        using var http = new HttpClient(stub) { BaseAddress = new Uri("http://127.0.0.1:8787/") };
        using var client = new AgentApiClient(http);
        Assert.False(await client.HealthAsync());
    }
}
