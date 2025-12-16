using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace Maanfee.WebSocket
{
    public class MaanfeeWebSocketClient : MaanfeeWebSocketBase, IMaanfeeWebSocketClient, IDisposable
    {
        public MaanfeeWebSocketClient(MaanfeeWebSocketOption options = null)
        {
            Options = options ?? new MaanfeeWebSocketOption();
            ServerUrl = $"ws://{Options.Host}:{Options.Port}/ws";
            WebSocketClient = new ClientWebSocket();
            CancellationTokenSource = new CancellationTokenSource();

            // Initialize state
            SetState(WebSocketClientState.Disconnected, "Initialized");
        }

        private MaanfeeWebSocketOption Options;
        private ClientWebSocket WebSocketClient;
        private readonly string ServerUrl;
        private Task _receivingTask;

        // Events
        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> ConnectionClosed;
        public event EventHandler<Exception> ErrorOccurred;
        public event EventHandler Connected;

        public bool IsConnected => WebSocketClient?.State == WebSocketState.Open &&
                   State == WebSocketClientState.Connected;

        public async Task ConnectAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MaanfeeWebSocketClient));

            if (!State.CanConnect())
                throw new InvalidOperationException($"Cannot connect. Current state: {State}");

            SetState(WebSocketClientState.Connecting, "Starting connection process");

            if (Options.AutoRetryConnection)
            {
                for (int i = 0; i < Options.RetryCount; i++)
                {
                    try
                    {
                        await InternalConnectAsync();
                        return;
                    }
                    catch (MaanfeeWebSocketException ex)
                    {
                        if (i == Options.RetryCount - 1)
                        {
                            SetState(WebSocketClientState.Faulted, $"Connection failed after {Options.RetryCount} attempts: {ex.Message}");
                            OnErrorOccurred(ex);
                            throw;
                        }

                        SetState(WebSocketClientState.Reconnecting, $"Retry {i + 1}/{Options.RetryCount} in {Options.RetryDelay.TotalSeconds}s");
                        await Task.Delay(Options.RetryDelay);
                    }
                }
            }
            else
            {
                try
                {
                    await InternalConnectAsync();
                }
                catch (MaanfeeWebSocketException ex)
                {
                    SetState(WebSocketClientState.Faulted, $"Connection failed: {ex.Message}");
                    OnErrorOccurred(ex);
                    throw;
                }
            }
        }

        private async Task InternalConnectAsync()
        {
            try
            {
                await WebSocketClient.ConnectAsync(new Uri(ServerUrl), CancellationTokenSource.Token);

                // بررسی وضعیت اتصال
                if (WebSocketClient.State != WebSocketState.Open)
                {
                    throw new MaanfeeWebSocketException($"WebSocket connection failed. State: {WebSocketClient.State}");
                }

                SetState(WebSocketClientState.Connected, "Successfully connected to server");
                OnConnected();
                Console.WriteLine("Connected to server");

                _receivingTask = Task.Run(StartReceiving);
            }
            catch (Exception ex) when (ex is not MaanfeeWebSocketException)
            {
                var maanfeeEx = new MaanfeeWebSocketException($"Connection failed: {ex.Message}", innerException: ex);
                OnErrorOccurred(maanfeeEx);
                throw maanfeeEx;
            }
            catch (MaanfeeWebSocketException ex)
            {
                OnErrorOccurred(ex);
                throw;
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_isDisposed)
                throw new MaanfeeWebSocketException($"{nameof(MaanfeeWebSocketClient)} is disposed");

            //if (_webSocket.State != WebSocketState.Open)
            //    throw new MaanfeeWebSocketException("WebSocket is not connected");
            if (!State.CanSend())
                throw new MaanfeeWebSocketException($"Cannot send message. Current state: {State}");

            var bytes = Encoding.UTF8.GetBytes(message);
            await WebSocketClient.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationTokenSource.Token);

            Console.WriteLine($"Sent: {message}");
        }

        private async Task StartReceiving()
        {
            // استفاده از ArrayPool برای مدیریت بهتر حافظه 
            // var buffer = new byte[DefaultBufferSize];
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);

            try
            {
                while (!CancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await WebSocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationTokenSource.Token);

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            OnMessageReceived(message);
                        }
                        else if (result.MessageType == WebSocketMessageType.Close)
                        {
                            SetState(WebSocketClientState.Disconnected, "Server closed connection");
                            OnConnectionClosed("Closed by server");
                            break;
                        }
                    }
                    catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                    {
                        SetState(WebSocketClientState.Disconnected, "Connection closed prematurely");
                        OnConnectionClosed("Connection closed");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                SetState(WebSocketClientState.Disconnected, "Operation cancelled");
            }
            catch (MaanfeeWebSocketException ex)
            {
                SetState(WebSocketClientState.Faulted, $"Error receiving: {ex.Message}");
                OnErrorOccurred(ex);
            }
            catch (Exception ex)
            {
                SetState(WebSocketClientState.Faulted, $"Unexpected error: {ex.Message}");
                OnErrorOccurred(ex);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_isDisposed)
                return;

            SetState(WebSocketClientState.Disconnecting, "Client initiated disconnect");

            CancellationTokenSource.Cancel();

            // 🔴 صبر کردن برای بسته شدن اتصال
            if (_receivingTask != null)
            {
                try
                {
                    await Task.WhenAny(
                        _receivingTask,
                        Task.Delay(TimeSpan.FromSeconds(5)));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during receive task completion: {ex.Message}");
                }
            }

            if (WebSocketClient.State == WebSocketState.Open)
            {
                await WebSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
            }

            SetState(WebSocketClientState.Disconnected, "Successfully disconnected");
            OnConnectionClosed("Disconnected by client");
            Console.WriteLine("Disconnected from server");
        }

        // Event invokers
        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, message);
        }

        protected virtual void OnConnectionClosed(string reason)
        {
            ConnectionClosed?.Invoke(this, reason);
        }

        protected virtual void OnErrorOccurred(Exception exception)
        {
            ErrorOccurred?.Invoke(this, exception);
        }

        protected virtual void OnConnected()
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    lock (StateLock)
                    {
                        if (_state != WebSocketClientState.Disposed)
                        {
                            SetState(WebSocketClientState.Disconnecting, "Disposing client");

                            try
                            {
                                CancellationTokenSource?.Cancel();

                                // صبر برای تکمیل کارهای در حال انجام
                                Task.Run(async () =>
                                {
                                    if (_receivingTask != null)
                                    {
                                        await Task.WhenAny(_receivingTask,
                                            Task.Delay(TimeSpan.FromSeconds(2)));
                                    }
                                }).Wait(TimeSpan.FromSeconds(3));
                            }
                            finally
                            {
                                WebSocketClient?.Dispose();
                                SetState(WebSocketClientState.Disposed, "Client disposed");
                            }
                        }
                    }
                }
                base.Dispose(disposing);
            }
        }

        // State management
        private WebSocketClientState _state = WebSocketClientState.Disconnected;

        public WebSocketClientState State
        {
            get
            {
                lock (StateLock) return _state;
            }
        }

        private void SetState(WebSocketClientState newState, string reason = null)
        {
            lock (StateLock)
            {
                if (_state == WebSocketClientState.Disposed && newState != WebSocketClientState.Disposed)
                    return; // Cannot change from Disposed

                var oldState = _state;
                _state = newState;
                OnStateChanged(oldState, newState, reason);
            }
        }

        public event EventHandler<WebSocketStateChangedEventArgs<WebSocketClientState>> StateChanged;

        protected virtual void OnStateChanged(WebSocketClientState oldState, WebSocketClientState newState, string reason = null)
        {
            Console.WriteLine($"[CLIENT] State changed: {oldState} -> {newState} ({reason})");
            StateChanged?.Invoke(this, new WebSocketStateChangedEventArgs<WebSocketClientState>
            {
                OldState = oldState,
                NewState = newState,
                Reason = reason
            });
        }

    }
}
