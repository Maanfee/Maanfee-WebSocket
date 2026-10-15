using System.Net.WebSockets;
using System.Text;

namespace Maanfee.WebSocket
{
    public sealed class WebSocketMessageSender : IWebSocketMessageSender
    {
        private readonly IWebSocketConnectionManager _connectionManager;

        public WebSocketMessageSender(IWebSocketConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        public async Task<bool> SendAsync(string connectionId, WebSocketPayloadType payloadType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            var connection = _connectionManager.Get(connectionId);
            if (connection == null || !connection.IsConnected)
                return false;

            var messageType = payloadType == WebSocketPayloadType.Text ? WebSocketMessageType.Text : WebSocketMessageType.Binary;
            await connection.SendAsync(payload, messageType, cancellationToken);
            return true;
        }

        public Task<bool> SendAsync(string connectionId, string message, CancellationToken cancellationToken = default) =>
            SendAsync(connectionId, WebSocketPayloadType.Text, Encoding.UTF8.GetBytes(message), cancellationToken);
    }
}
