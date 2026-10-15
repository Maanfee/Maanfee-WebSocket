using Maanfee.WebSocket;
using System.Collections.Concurrent;

namespace WebAPIServer;

/// <summary>
/// The chat logic of this example (the library only moves messages):
///  - a line typed by a client is logged as "Client: &lt;name&gt;: &lt;text&gt;" and relayed to every OTHER client;
///  - a line sent through the HTTP API is sent to ALL clients as "Server: WebAPI: &lt;text&gt;".
/// </summary>
public sealed class ChatRelay : IHostedService
{
    private const string Name = "WebAPI";
    private const int MaxStoredMessages = 100;

    private readonly MaanfeeWebSocketServer _server;
    private readonly IWebSocketConnectionManager _connections;
    private readonly IWebSocketMessageSender _sender;
    private readonly ILogger<ChatRelay> _logger;
    private readonly ConcurrentQueue<string> _log = new();

    public ChatRelay(MaanfeeWebSocketServer server, IWebSocketConnectionManager connections,
                     IWebSocketMessageSender sender, ILogger<ChatRelay> logger)
    {
        _server = server;
        _connections = connections;
        _sender = sender;
        _logger = logger;
    }

    public int ConnectedCount => _connections.GetAll().Count(c => c.IsConnected);

    public IReadOnlyList<string> Log => _log.ToArray();

    /// <summary>Sends a line from the server to every connected client.</summary>
    public async Task SendToAllAsync(string text)
    {
        var line = $"Server: {Name}: {text}";
        Record(line);
        await SendAsync(line, exceptConnectionId: null);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _server.MaanfeeClientConnected += (_, e) => _logger.LogInformation("[+] Client joined ({Count} connected)", _connections.Count);
        _server.MaanfeeClientDisconnected += (_, e) => _logger.LogInformation("[-] Client left ({Count} connected)", _connections.Count);
        _server.MaanfeeMessageReceived += OnMessageReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server.MaanfeeMessageReceived -= OnMessageReceived;
        return Task.CompletedTask;
    }

    private async void OnMessageReceived(object? sender, WebSocketMessage message)
    {
        if (message.PayloadType != WebSocketPayloadType.Text)
            return;

        try
        {
            var line = $"Client: {message.AsText()}";   // the client already prefixed its own name
            Record(line);
            await SendAsync(line, exceptConnectionId: message.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError("Relay failed: {Error}", ex.Message);
        }
    }

    private Task SendAsync(string line, string? exceptConnectionId) =>
        Task.WhenAll(_connections.GetAll()
            .Where(c => c.Id != exceptConnectionId)
            .Select(c => _sender.SendAsync(c.Id, line)));

    private void Record(string line)
    {
        _logger.LogInformation("{Line}", line);
        _log.Enqueue(line);
        while (_log.Count > MaxStoredMessages)
            _log.TryDequeue(out string? _);
    }
}
