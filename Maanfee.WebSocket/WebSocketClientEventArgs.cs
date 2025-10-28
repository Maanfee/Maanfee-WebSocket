using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public class WebSocketClientEventArgs : EventArgs
    {
        public WS WebSocket { get; set; }

        public WebSocketUser User { get; set; }
    }
}
