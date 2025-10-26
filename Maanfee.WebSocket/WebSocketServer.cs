using System.Net.WebSockets;
using System.Text;
using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public class WebSocketServer : IDisposable
    {
        private readonly List<WS> _connectedClients = new List<WS>();
        private readonly object _lock = new object();
        private bool _isRunning = false;
        private CancellationTokenSource _cancellationTokenSource;

        // Events
        public event EventHandler<WebSocketClientEventArgs> ClientConnected;
        public event EventHandler<WebSocketClientEventArgs> ClientDisconnected;
        public event EventHandler ServerStopped;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public WebSocketServer()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Console.WriteLine("WebSocket Server initialized");
        }

        public async Task HandleWebSocketConnection(WS webSocket)
        {
            var clientId = Guid.NewGuid().ToString();

            lock (_lock)
            {
                _connectedClients.Add(webSocket);
            }

            Console.WriteLine($"Client connected. Total clients: {_connectedClients.Count}");

            // Trigger ClientConnected event
            OnClientConnected(new WebSocketClientEventArgs
            {
                ClientId = clientId,
                WebSocket = webSocket,
                ConnectedTime = DateTime.Now
            });

            await HandleClientAsync(webSocket, clientId);
        }

        private async Task HandleClientAsync(WS webSocket, string clientId)
        {
            var buffer = new byte[4096];

            try
            {
                while (webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"Received from client {clientId}: {message}");

                        // Trigger MessageReceived event
                        OnMessageReceived(new MessageReceivedEventArgs
                        {
                            ClientId = clientId,
                            Message = message,
                            ReceivedTime = DateTime.Now,
                            WebSocket = webSocket
                        });

                        // ارسال پیام به همه کلاینت‌ها
                        await BroadcastMessage($"Broadcast: {message}");
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Server is stopping - this is normal
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client handling error: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _connectedClients.Remove(webSocket);
                }

                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                }

                webSocket.Dispose();

                Console.WriteLine($"Client disconnected. Total clients: {_connectedClients.Count}");

                // Trigger ClientDisconnected event
                OnClientDisconnected(new WebSocketClientEventArgs
                {
                    ClientId = clientId,
                    WebSocket = webSocket,
                    DisconnectedTime = DateTime.Now
                });
            }
        }

        private async Task BroadcastMessage(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var tasks = new List<Task>();

            lock (_lock)
            {
                foreach (var client in _connectedClients.Where(c => c.State == WebSocketState.Open))
                {
                    tasks.Add(client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None));
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task SendToClientAsync(string clientId, string message)
        {
            var client = GetClientById(clientId);
            if (client != null && client.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public async Task SendToAllAsync(string message)
        {
            await BroadcastMessage(message);
        }

        public void Start()
        {
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            Console.WriteLine("WebSocket Server started");
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _cancellationTokenSource.Cancel();

            // Close all client connections
            var closeTasks = new List<Task>();
            lock (_lock)
            {
                foreach (var client in _connectedClients.Where(c => c.State == WebSocketState.Open))
                {
                    closeTasks.Add(client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopping", CancellationToken.None));
                }
            }

            await Task.WhenAll(closeTasks);

            // Trigger ServerStopped event
            OnServerStopped();

            Console.WriteLine("WebSocket Server stopped");
        }

        public int GetConnectedClientsCount()
        {
            lock (_lock)
            {
                return _connectedClients.Count;
            }
        }

        public List<string> GetConnectedClientIds()
        {
            // در این پیاده‌سازی ساده، clientIdها را ذخیره نکرده‌ایم
            // در یک پیاده‌سازی واقعی، باید clientIdها را در یک dictionary نگهداری کنید
            lock (_lock)
            {
                return Enumerable.Range(0, _connectedClients.Count)
                    .Select(i => $"Client_{i}")
                    .ToList();
            }
        }

        private WS GetClientById(string clientId)
        {
            // در این پیاده‌سازی ساده، این متد کارایی محدودی دارد
            // در پیاده‌سازی واقعی، باید mapping بین clientId و WebSocket را نگهداری کنید
            lock (_lock)
            {
                // این فقط یک نمونه ساده است
                return _connectedClients.FirstOrDefault();
            }
        }

        // Event invokers
        protected virtual void OnClientConnected(WebSocketClientEventArgs e)
        {
            ClientConnected?.Invoke(this, e);
        }

        protected virtual void OnClientDisconnected(WebSocketClientEventArgs e)
        {
            ClientDisconnected?.Invoke(this, e);
        }

        protected virtual void OnServerStopped()
        {
            ServerStopped?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnMessageReceived(MessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (_isRunning)
            {
                StopAsync().Wait(5000); // Wait up to 5 seconds for graceful shutdown
            }

            _cancellationTokenSource?.Dispose();
        }
    }
}