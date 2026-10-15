using Maanfee.WebSocket;
using System.Collections.Concurrent;

namespace WebAPIClient;

/// <summary>
/// Keeps one chat connection alive for the lifetime of the Web API and remembers what it received.
/// </summary>
public sealed class ChatClientService : BackgroundService
{
    private const string Name = "WebAPI";
    private const int MaxStoredMessages = 100;

    private readonly MaanfeeWebSocketClient _client;
    private readonly ILogger<ChatClientService> _logger;
    private readonly ConcurrentQueue<string> _messages = new();

    public ChatClientService(ILogger<ChatClientService> logger)
    {
        _logger = logger;
        _client = new MaanfeeWebSocketClient(new MaanfeeWebSocketClientOptions
        {
            Host = "127.0.0.1",
            Port = 5000,
            Path = "/ws",
            AutoRetryConnection = true,
            RetryCount = 10,
            RetryDelay = TimeSpan.FromSeconds(2),
        });

        _client.MessageReceived += (_, text) =>
        {
            _logger.LogInformation("{Message}", text);
            _messages.Enqueue(text);
            while (_messages.Count > MaxStoredMessages)
                _messages.TryDequeue(out string _);
        };
        _client.ConnectionClosed += (_, reason) => _logger.LogWarning("Connection closed: {Reason}", reason);
    }

    public bool IsConnected => _client.IsConnected;

    public WebSocketClientState State => _client.State;

    public IReadOnlyList<string> Messages => _messages.ToArray();

    /// <summary>Sends a chat line. The server shows it as "Client: WebAPI: ..." and relays it to the other clients.</summary>
    public Task SendAsync(string message, CancellationToken cancellationToken = default) =>
        _client.SendTextAsync($"{Name}: {message}", cancellationToken);

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await _client.DisconnectAsync();
        await _client.ConnectAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _client.ConnectAsync(stoppingToken);
            _logger.LogInformation("Connected to the chat server");
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _logger.LogError("Could not connect to the chat server: {Error}", ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await _client.DisposeAsync();
    }
}
