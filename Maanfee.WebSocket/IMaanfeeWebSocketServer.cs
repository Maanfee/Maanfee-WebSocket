using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public interface IMaanfeeWebSocketServer
    {
        void Start();

        Task StopAsync();

        Task HandleWebSocketConnectionAsync(WS webSocket);

        Task SendToClientAsync(string clientId, string message);

        Task SendToAllAsync(string message);

        void Dispose();

        // ************************************

        int GetConnectedUsersCount();

        List<string> GetConnectedUserIds();

        MaanfeeWebSocketUser GetUserById(string userId);

        List<MaanfeeWebSocketUser> GetAllUsers();

        // ************************************

        // Events
        event EventHandler<MaanfeeWebSocketClientEventArgs> MaanfeeClientConnected;
        event EventHandler<MaanfeeWebSocketClientEventArgs> MaanfeeClientDisconnected;
        event EventHandler MaanfeeServerStopped;
        event EventHandler<MaanfeeMessageReceivedEventArgs> MaanfeeMessageReceived;

        // State management
        WebSocketServerState State { get; }

        // اضافه کردن event برای StateChanged
        event EventHandler<WebSocketStateChangedEventArgs<WebSocketServerState>> StateChanged;
    }
}
