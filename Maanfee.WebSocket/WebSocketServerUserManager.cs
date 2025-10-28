using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public partial class WebSocketServer
    {
        private readonly List<WebSocketUser> Users = new List<WebSocketUser>();

        public int GetConnectedUsersCount()
        {
            lock (_lock)
            {
                return Users.Count(u => u.IsConnected);
            }
        }

        public List<string> GetConnectedUserIds()
        {
            lock (_lock)
            {
                return Users.Where(u => u.IsConnected).Select(u => u.Id).ToList();
            }
        }

        public WebSocketUser GetUserById(string userId)
        {
            lock (_lock)
            {
                return Users.FirstOrDefault(u => u.Id == userId);
            }
        }

        public List<WebSocketUser> GetAllUsers()
        {
            lock (_lock)
            {
                return Users.ToList();
            }
        }
    }
}