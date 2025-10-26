using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public string ClientId { get; set; }
        public string Message { get; set; }
        public DateTime ReceivedTime { get; set; }
        public WS WebSocket { get; set; }
    }
}
