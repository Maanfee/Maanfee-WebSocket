using System.Net.WebSockets;
using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public partial class MaanfeeWebSocketServer : MaanfeeWebSocketBase, IMaanfeeWebSocketServer, IDisposable
    {
        public MaanfeeWebSocketServer()
        {
            CancellationTokenSource = new CancellationTokenSource();
            Console.WriteLine("WebSocket Server initialized");
        }

        private readonly object _lock = new object();

        // Events
        public event EventHandler<MaanfeeWebSocketClientEventArgs> MaanfeeClientConnected;
        public event EventHandler<MaanfeeWebSocketClientEventArgs> MaanfeeClientDisconnected;
        public event EventHandler MaanfeeServerStopped;
        public event EventHandler<MaanfeeMessageReceivedEventArgs> MaanfeeMessageReceived;

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
            OnMaanfeeClientConnected(new MaanfeeWebSocketClientEventArgs
            {
                WebSocket = webSocket,
                User = user
            });

            await ReceiveMessageFromClientAsync(user);
        }

        public void Start()
        {
            if (!State.CanStart())
                throw new InvalidOperationException($"Cannot start server. Current state: {State}");

            SetState(WebSocketServerState.Starting, "Starting WebSocket server");

            try
            {
                CancellationTokenSource = new CancellationTokenSource();
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
                CancellationTokenSource.Cancel();

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
                OnMaanfeeServerStopped();

                Console.WriteLine("WebSocket Server stopped");
            }
            catch (Exception ex)
            {
                SetState(WebSocketServerState.Faulted, $"Error while stopping: {ex.Message}");
                throw;
            }
            finally
            {
                lock (_lock)
                {
                    Users.Clear();
                }
            }
        }

        // Event invokers
        protected virtual void OnMaanfeeClientConnected(MaanfeeWebSocketClientEventArgs e)
        {
            MaanfeeClientConnected?.Invoke(this, e);
        }

        protected virtual void OnMaanfeeClientDisconnected(MaanfeeWebSocketClientEventArgs e)
        {
            MaanfeeClientDisconnected?.Invoke(this, e);
        }

        protected virtual void OnMaanfeeServerStopped()
        {
            MaanfeeServerStopped?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnMaanfeeMessageReceived(MaanfeeMessageReceivedEventArgs e)
        {
            MaanfeeMessageReceived?.Invoke(this, e);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    if (_state == WebSocketServerState.Running || _state == WebSocketServerState.Starting)
                    {
                        SetState(WebSocketServerState.Stopping, "Disposing server");

                        try
                        {
                            var stopTask = Task.Run(async () => await StopAsync().ConfigureAwait(false));
                            if (!stopTask.Wait(TimeSpan.FromSeconds(5)))
                            {
                                Console.WriteLine("Warning: Server stop timed out during disposal");
                                // Force cancellation
                                CancellationTokenSource?.Cancel();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error stopping server during disposal: {ex.Message}");
                        }
                    }

                    CancellationTokenSource?.Dispose();
                    SetState(WebSocketServerState.Stopped, "Server disposed");
                }

                // کلاس پایه هم Dispose کند
                base.Dispose(disposing);
            }
        }

        // State management
        private WebSocketServerState _state = WebSocketServerState.Stopped;

        public WebSocketServerState State
        {
            get
            {
                lock (StateLock)
                    return _state;
            }
        }

        private void SetState(WebSocketServerState newState, string reason = null)
        {
            lock (StateLock)
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