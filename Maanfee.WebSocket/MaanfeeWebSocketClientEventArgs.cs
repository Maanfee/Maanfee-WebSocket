using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public class MaanfeeWebSocketClientEventArgs : EventArgs
    {
        public WS WebSocket { get; set; }

        public MaanfeeWebSocketUser User { get; set; }
    }
}
