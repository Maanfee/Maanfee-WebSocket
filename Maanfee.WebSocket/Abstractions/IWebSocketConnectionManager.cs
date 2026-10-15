#nullable enable
namespace Maanfee.WebSocket
{
    public interface IWebSocketConnectionManager
    {
        int Count { get; }

        void Add(WebSocketConnection connection);

        bool Remove(string connectionId);

        WebSocketConnection? Get(string connectionId);

        IReadOnlyList<WebSocketConnection> GetAll();

        void Clear();
    }
}
