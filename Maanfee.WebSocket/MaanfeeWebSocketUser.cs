using System.Net.WebSockets;
using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public class MaanfeeWebSocketUser
    {
        public string Id { get; set; }

        public WS WebSocket { get; set; }

        public DateTime ConnectedTime { get; set; }

        public DateTime? DisconnectedTime { get; set; }

        public string ConnectionInfo { get; set; }

        public MaanfeeWebSocketUser(string id, WS webSocket)
        {
            Id = id;
            WebSocket = webSocket;
            ConnectedTime = DateTime.Now;
        }

        public bool IsConnected => WebSocket?.State == WebSocketState.Open;

        public void MarkDisconnected()
        {
            DisconnectedTime = DateTime.Now;
        }
    }
}
