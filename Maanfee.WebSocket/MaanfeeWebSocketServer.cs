using System.Net.WebSockets;
using System.Text;
using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public partial class MaanfeeWebSocketServer : IMaanfeeWebSocketServer, IDisposable
    {
        public MaanfeeWebSocketServer(MaanfeeWebSocketOption options = null)
        {
            _options = options ?? new MaanfeeWebSocketOption();
            _cancellationTokenSource = new CancellationTokenSource();
            Console.WriteLine("WebSocket Server initialized");
        }

        protected MaanfeeWebSocketOption _options;
        private readonly object _lock = new object();
        private CancellationTokenSource _cancellationTokenSource;

        // Events
        public event EventHandler<MaanfeeWebSocketClientEventArgs> ClientConnected;
        public event EventHandler<MaanfeeWebSocketClientEventArgs> ClientDisconnected;
        public event EventHandler ServerStopped;
        public event EventHandler<MaanfeeMessageReceivedEventArgs> MessageReceived;

        public virtual async Task HandleWebSocketConnectionAsync(WS webSocket)
        {
            if (!State.CanAcceptConnections())
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Server not accepting connections", CancellationToken.None);
                return;
            }

            var userId = Guid.NewGuid().ToString();
            var user = new MaanfeeWebSocketUser(userId, webSocket);

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
            OnClientConnected(new MaanfeeWebSocketClientEventArgs
            {
                WebSocket = webSocket,
                User = user
            });

            await HandleUserAsync(user);
        }

        protected virtual async Task HandleUserAsync(MaanfeeWebSocketUser user)
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
                        OnMessageReceived(new MaanfeeMessageReceivedEventArgs
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
            catch (MaanfeeWebSocketException ex)
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

        protected virtual async Task CleanupUser(MaanfeeWebSocketUser user)
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
                    catch (MaanfeeWebSocketException)
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
                OnClientDisconnected(new MaanfeeWebSocketClientEventArgs
                {
                    WebSocket = user.WebSocket,
                    User = user
                });
            }
        }

        protected virtual async Task BroadcastMessage(string message)
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

        public virtual async Task SendToClientAsync(string clientId, string message)
        {
            var user = GetUserById(clientId);
            if (user != null && user.IsConnected)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await user.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public virtual async Task SendToAllAsync(string message)
        {
            await BroadcastMessage(message);
        }

        public void Start()
        {
            if (!State.CanStart())
                throw new InvalidOperationException($"Cannot start server. Current state: {State}");

            SetState(WebSocketServerState.Starting, "Starting WebSocket server");

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                SetState(WebSocketServerState.Running, "Server is now running and accepting connections");
                Console.WriteLine("WebSocket Server started");
            }
            catch (Exception ex)
            {
                SetState(WebSocketServerState.Faulted, $"Failed to start: {ex.Message}");
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!State.CanStop())
                return;

            SetState(WebSocketServerState.Stopping, "Stopping WebSocket server");

            try
            {
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

                SetState(WebSocketServerState.Stopped, "Server stopped successfully");
                // Trigger ServerStopped event
                OnServerStopped();

                Console.WriteLine("WebSocket Server stopped");
            }
            catch (Exception ex)
            {
                SetState(WebSocketServerState.Faulted, $"Error while stopping: {ex.Message}");
                throw;
            }
        }

        // Event invokers
        protected virtual void OnClientConnected(MaanfeeWebSocketClientEventArgs e)
        {
            ClientConnected?.Invoke(this, e);
        }

        protected virtual void OnClientDisconnected(MaanfeeWebSocketClientEventArgs e)
        {
            ClientDisconnected?.Invoke(this, e);
        }

        protected virtual void OnServerStopped()
        {
            ServerStopped?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnMessageReceived(MaanfeeMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (_state == WebSocketServerState.Running || _state == WebSocketServerState.Starting)
            {
                SetState(WebSocketServerState.Stopping, "Disposing server");
                StopAsync().Wait(5000);
            }

            _cancellationTokenSource?.Dispose();
            SetState(WebSocketServerState.Stopped, "Server disposed");
        }

        // State management
        private WebSocketServerState _state = WebSocketServerState.Stopped;
        private readonly object _stateLock = new object();

        public WebSocketServerState State
        {
            get
            {
                lock (_stateLock)
                    return _state;
            }
        }

        private void SetState(WebSocketServerState newState, string reason = null)
        {
            lock (_stateLock)
            {
                var oldState = _state;
                _state = newState;
                OnStateChanged(oldState, newState, reason);
            }
        }

        public event EventHandler<WebSocketStateChangedEventArgs<WebSocketServerState>> StateChanged;

        protected virtual void OnStateChanged(WebSocketServerState oldState, WebSocketServerState newState, string reason = null)
        {
            Console.WriteLine($"[SERVER] State changed: {oldState} -> {newState} ({reason})");
            StateChanged?.Invoke(this, new WebSocketStateChangedEventArgs<WebSocketServerState>
            {
                OldState = oldState,
                NewState = newState,
                Reason = reason
            });
        }
    }
}