using System.Net.WebSockets;

namespace Maanfee.WebSocket
{
    public class WebSocketException : Exception
    {
        public WebSocketCloseStatus? CloseStatus { get; }

        public WebSocketException(string message, WebSocketCloseStatus? closeStatus = null) : base(message)
        {
            CloseStatus = closeStatus;
        }
    }
}
