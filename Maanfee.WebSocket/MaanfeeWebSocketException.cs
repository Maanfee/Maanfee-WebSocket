using System.Net.WebSockets;

namespace Maanfee.WebSocket
{
    public class MaanfeeWebSocketException : Exception
    {
        public WebSocketCloseStatus? CloseStatus { get; }
        public WebSocketClientState? ClientState { get; }
        public WebSocketServerState? ServerState { get; }

        public MaanfeeWebSocketException(string message,
            WebSocketCloseStatus? closeStatus = null,
            Exception innerException = null,
            WebSocketClientState? clientState = null,
            WebSocketServerState? serverState = null)
            : base(message, innerException)
        {
            CloseStatus = closeStatus;
            ClientState = clientState;
            ServerState = serverState;
        }
    }
}
