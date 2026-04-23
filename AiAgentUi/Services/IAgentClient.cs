namespace AiAgentUi.Services;

public interface IAgentClient
{
    Task<bool> HealthAsync(CancellationToken cancellationToken = default);
    Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default);
}

