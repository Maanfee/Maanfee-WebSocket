using System.Net.WebSockets;
using System.Text;
using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public partial class WebSocketServer : IWebSocketServer, IDisposable
    {
        public WebSocketServer(WebSocketOption options = null)
        {
            _options = options ?? new WebSocketOption();
            _cancellationTokenSource = new CancellationTokenSource();
            Console.WriteLine("WebSocket Server initialized");
        }

        private WebSocketOption _options;
        private readonly object _lock = new object();
        private bool _isRunning = false;
        private CancellationTokenSource _cancellationTokenSource;

        // Events
        public event EventHandler<WebSocketClientEventArgs> ClientConnected;
        public event EventHandler<WebSocketClientEventArgs> ClientDisconnected;
        public event EventHandler ServerStopped;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public async Task HandleWebSocketConnectionAsync(WS webSocket)
        {
            var userId = Guid.NewGuid().ToString();
            var user = new WebSocketUser(userId, webSocket);

            // بررسی وضعیت WebSocket قبل از اضافه کردن
            if (webSocket.State != WebSocketState.Open)
            {
                Console.WriteLine($"WebSocket is not open. State: {webSocket.State}");
                return;
            }

            lock (_lock)
            {
                Users.Add(user);
            }

            Console.WriteLine($"User connected: {userId}. Total users: {Users.Count}");

            // Trigger ClientConnected event
            OnClientConnected(new WebSocketClientEventArgs
            {
                WebSocket = webSocket,
                User = user
            });

            await HandleUserAsync(user);
        }

        private async Task HandleUserAsync(WebSocketUser user)
        {
            var buffer = new byte[_options.BufferSize];

            try
            {
                while (user.IsConnected && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await user.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"Received from user {user.Id}: {message}");

                        // Trigger MessageReceived event
                        OnMessageReceived(new MessageReceivedEventArgs
                        {
                            Message = message,
                            ReceivedTime = DateTime.Now,
                            WebSocket = user.WebSocket,
                            User = user
                        });

                        // ارسال پیام به همه کاربران با مدیریت خطا
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await BroadcastMessage($"Broadcast: {message}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Broadcast error: {ex.Message}");
                            }
                        });
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // فقط اتصال را قطع کنید بدون فراخوانی CloseAsync مجدد
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Server is stopping - this is normal
                Console.WriteLine($"Operation canceled for user {user.Id}");
            }
            catch (System.Net.WebSockets.WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                // User closed connection unexpectedly
                Console.WriteLine($"User {user.Id} closed connection prematurely: {ex.Message}");
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"WebSocket error for user {user.Id}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error for user {user.Id}: {ex.Message}");
            }
            finally
            {
                await CleanupUser(user);
            }
        }

        private async Task CleanupUser(WebSocketUser user)
        {
            try
            {
                user.MarkDisconnected();

                lock (_lock)
                {
                    // ✅ بررسی وجود کاربر قبل از حذف
                    if (Users.Contains(user))
                    {
                        Users.Remove(user);
                    }
                }

                if (user.WebSocket.State == WebSocketState.Open || user.WebSocket.State == WebSocketState.CloseReceived || user.WebSocket.State == WebSocketState.CloseSent)
                {
                    try
                    {
                        await user.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "Connection closed",
                            CancellationToken.None);
                    }
                    catch (WebSocketException)
                    {
                        // Ignore close errors during cleanup
                    }
                    catch (ObjectDisposedException)
                    {
                        // Socket already disposed
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during user cleanup {user.Id}: {ex.Message}");
            }
            finally
            {
                try
                {
                    user.WebSocket.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing websocket for {user.Id}: {ex.Message}");
                }

                Console.WriteLine($"User disconnected: {user.Id}. Total users: {Users.Count}");
                OnClientDisconnected(new WebSocketClientEventArgs
                {
                    WebSocket = user.WebSocket,
                    User = user
                });
            }
        }

        private async Task BroadcastMessage(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var tasks = new List<Task>();

            lock (_lock)
            {
                foreach (var user in Users.Where(u => u.IsConnected))
                {
                    tasks.Add(user.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None));
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task SendToClientAsync(string clientId, string message)
        {
            var user = GetUserById(clientId);
            if (user != null && user.IsConnected)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await user.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
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

            // Close all user connections
            var closeTasks = new List<Task>();
            lock (_lock)
            {
                foreach (var user in Users.Where(u => u.IsConnected))
                {
                    closeTasks.Add(user.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopping", CancellationToken.None));
                }
            }

            await Task.WhenAll(closeTasks);

            // Trigger ServerStopped event
            OnServerStopped();

            Console.WriteLine("WebSocket Server stopped");
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