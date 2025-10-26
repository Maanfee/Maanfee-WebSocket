using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public class WebSocketClientEventArgs : EventArgs
    {
        public string ClientId { get; set; }
        public WS WebSocket { get; set; }
        public DateTime ConnectedTime { get; set; }
        public DateTime DisconnectedTime { get; set; }
    }
}
