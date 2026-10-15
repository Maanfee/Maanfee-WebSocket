using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public interface IMaanfeeWebSocketServer : IDisposable, IAsyncDisposable
    {
        void Start();

        Task StopAsync();

        Task HandleWebSocketConnectionAsync(WS webSocket);

        // ************************************

        // Events
        event EventHandler<MaanfeeWebSocketClientEventArgs> MaanfeeClientConnected;
        event EventHandler<MaanfeeWebSocketClientEventArgs> MaanfeeClientDisconnected;
        event EventHandler MaanfeeServerStopped;
        event EventHandler<WebSocketMessage> MaanfeeMessageReceived;

        // State management
        WebSocketServerState State { get; }

        // اضافه کردن event برای StateChanged
        event EventHandler<WebSocketStateChangedEventArgs<WebSocketServerState>> StateChanged;
    }
}
