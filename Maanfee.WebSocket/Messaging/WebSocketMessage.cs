using System.Text;

namespace Maanfee.WebSocket
{
    public sealed class WebSocketMessage
    {
        public string ConnectionId { get; }

        public WebSocketPayloadType PayloadType { get; }

        public ReadOnlyMemory<byte> Payload { get; }

        public DateTime ReceivedAt { get; }

        public WebSocketMessage(string connectionId, WebSocketPayloadType payloadType, ReadOnlyMemory<byte> payload)
        {
            ConnectionId = connectionId;
            PayloadType = payloadType;
            Payload = payload;
            ReceivedAt = DateTime.UtcNow;
        }

        public string AsText() => Encoding.UTF8.GetString(Payload.Span);
    }
}
