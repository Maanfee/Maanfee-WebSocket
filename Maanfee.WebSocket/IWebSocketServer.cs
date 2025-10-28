using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public interface IWebSocketServer
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

        WebSocketUser GetUserById(string userId);

        List<WebSocketUser> GetAllUsers();

        // ************************************

        // Events
        event EventHandler<WebSocketClientEventArgs> ClientConnected;
        event EventHandler<WebSocketClientEventArgs> ClientDisconnected;
        event EventHandler ServerStopped;
        event EventHandler<MessageReceivedEventArgs> MessageReceived;
    }
}
