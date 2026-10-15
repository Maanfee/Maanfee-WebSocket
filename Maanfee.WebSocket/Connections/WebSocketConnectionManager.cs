#nullable enable
using System.Collections.Concurrent;

namespace Maanfee.WebSocket
{
    public class WebSocketConnectionManager : IWebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();

        public int Count => _connections.Count;

        public void Add(WebSocketConnection connection)
        {
            ArgumentNullException.ThrowIfNull(connection);
            _connections[connection.Id] = connection;
        }

        public bool Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

        public WebSocketConnection? Get(string connectionId) =>
            _connections.TryGetValue(connectionId, out var connection) ? connection : null;

        public IReadOnlyList<WebSocketConnection> GetAll() => _connections.Values.ToList();

        public void Clear() => _connections.Clear();
    }
}
