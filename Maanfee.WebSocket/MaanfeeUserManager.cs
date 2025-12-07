using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public partial class MaanfeeWebSocketServer
    {
        protected readonly List<MaanfeeWebSocketUser> Users = new List<MaanfeeWebSocketUser>();

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

        public MaanfeeWebSocketUser GetUserById(string userId)
        {
            lock (_lock)
            {
                return Users.FirstOrDefault(u => u.Id == userId);
            }
        }

        public List<MaanfeeWebSocketUser> GetAllUsers()
        {
            lock (_lock)
            {
                return Users.ToList();
            }
        }
    }
}