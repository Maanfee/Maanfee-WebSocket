using System.Net.WebSockets;
using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public sealed class WebSocketConnection : IDisposable
    {
        // WebSocket allows only one outstanding SendAsync at a time.
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public string Id { get; }

        private WS WebSocket { get; }

        public DateTime ConnectedAt { get; }

        public DateTime? DisconnectedAt { get; private set; }

        public WebSocketState State => WebSocket?.State ?? WebSocketState.None;

        public bool IsConnected => State == WebSocketState.Open;

        public WebSocketConnection(string id, WS webSocket)
        {
            Id = id;
            WebSocket = webSocket;
            ConnectedAt = DateTime.UtcNow;
        }

        /// <summary>Sends one message; concurrent callers are serialized.</summary>
        public async Task SendAsync(ReadOnlyMemory<byte> payload, WebSocketMessageType messageType, CancellationToken cancellationToken = default)
        {
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await WebSocket.SendAsync(payload, messageType, true, cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>Receives one frame. Only one receive loop per connection is supported.</summary>
        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken = default) =>
            WebSocket.ReceiveAsync(buffer, cancellationToken);

        /// <summary>Closes the connection; serialized with sends.</summary>
        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken = default)
        {
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                // Another path (server stop vs. receive-loop cleanup) may have closed it already.
                if (WebSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
                    return;

                await WebSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Dispose() => WebSocket?.Dispose();

        public void MarkDisconnected()
        {
            DisconnectedAt = DateTime.UtcNow;
        }
    }
}
