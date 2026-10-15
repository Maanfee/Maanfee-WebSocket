namespace Maanfee.WebSocket
{
    public interface IMaanfeeWebSocketClient : IDisposable, IAsyncDisposable
    {
        Task ConnectAsync(CancellationToken cancellationToken = default);

        Task SendAsync(WebSocketPayloadType payloadType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

        Task SendTextAsync(string message, CancellationToken cancellationToken = default);

        Task SendBinaryAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

        Task DisconnectAsync();

        // Events
        /// <summary>Text messages only, decoded as UTF-8.</summary>
        event EventHandler<string> MessageReceived;

        /// <summary>Every complete message, text or binary, with its raw payload.</summary>
        event EventHandler<WebSocketMessage> RawMessageReceived;
        event EventHandler<string> ConnectionClosed;
        event EventHandler<Exception> ErrorOccurred;
        event EventHandler Connected;

        // State management
        WebSocketClientState State { get; }
        event EventHandler<WebSocketStateChangedEventArgs<WebSocketClientState>> StateChanged;
    }
}
