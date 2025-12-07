using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public class MaanfeeMessageReceivedEventArgs : EventArgs
    {
        public string Message { get; set; }

        public DateTime ReceivedTime { get; set; }

        public WS WebSocket { get; set; }

        public MaanfeeWebSocketUser User { get; set; }
    }
}
