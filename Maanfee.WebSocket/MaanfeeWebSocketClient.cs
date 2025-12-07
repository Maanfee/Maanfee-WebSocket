using System.Net.WebSockets;
using System.Text;

namespace Maanfee.WebSocket
{
    public class MaanfeeWebSocketClient : IMaanfeeWebSocketClient, IDisposable
    {
        public MaanfeeWebSocketClient(MaanfeeWebSocketOption options = null)
        {
            _options = options ?? new MaanfeeWebSocketOption();
            _serverUrl = $"ws://{_options.Host}:{_options.Port}/ws";
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            // Initialize state
            SetState(WebSocketClientState.Disconnected, "Initialized");
        }

        private MaanfeeWebSocketOption _options;
        private ClientWebSocket _webSocket;
        private readonly string _serverUrl;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed = false;

        // Events
        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> ConnectionClosed;
        public event EventHandler<Exception> ErrorOccurred;
        public event EventHandler Connected;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        public async Task ConnectAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MaanfeeWebSocketClient));

            if (!State.CanConnect())
                throw new InvalidOperationException($"Cannot connect. Current state: {State}");

            SetState(WebSocketClientState.Connecting, "Starting connection process");

            if (_options.AutoRetryConnection)
            {
                for (int i = 0; i < _options.RetryCount; i++)
                {
                    try
                    {
                        await InternalConnectAsync();
                        return;
                    }
                    catch (MaanfeeWebSocketException ex)
                    {
                        if (i == _options.RetryCount - 1)
                        {
                            SetState(WebSocketClientState.Faulted, $"Connection failed after {_options.RetryCount} attempts: {ex.Message}");
                            OnErrorOccurred(ex);
                            throw;
                        }

                        SetState(WebSocketClientState.Reconnecting, $"Retry {i + 1}/{_options.RetryCount} in {_options.RetryDelay.TotalSeconds}s");
                        await Task.Delay(_options.RetryDelay);
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
                await _webSocket.ConnectAsync(new Uri(_serverUrl), CancellationToken.None);
                SetState(WebSocketClientState.Connected, "Successfully connected to server");
                OnConnected();
                Console.WriteLine("Connected to server");

                _ = Task.Run(StartReceiving);
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
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);

            Console.WriteLine($"Sent: {message}");
        }

        private async Task StartReceiving()
        {
            var buffer = new byte[_options.BufferSize];

            while (!_cancellationTokenSource.Token.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

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
                catch (OperationCanceledException)
                {
                    SetState(WebSocketClientState.Disconnected, "Operation cancelled");
                    break;
                }
                catch (MaanfeeWebSocketException ex)
                {
                    SetState(WebSocketClientState.Faulted, $"Error receiving: {ex.Message}");
                    OnErrorOccurred(ex);
                    break;
                }
                catch (Exception ex)
                {
                    SetState(WebSocketClientState.Faulted, $"Unexpected error: {ex.Message}");
                    OnErrorOccurred(ex);
                    break;
                }
            }
        }

        public async Task DisconnectAsync()
        {
            if (_isDisposed) return;

            SetState(WebSocketClientState.Disconnecting, "Client initiated disconnect");

            _cancellationTokenSource.Cancel();

            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
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

        public void Dispose()
        {
            if (!_isDisposed)
            {
                SetState(WebSocketClientState.Disconnecting, "Disposing client");

                _cancellationTokenSource?.Cancel();
                _webSocket?.Dispose();
                _cancellationTokenSource?.Dispose();

                SetState(WebSocketClientState.Disposed, "Client disposed");
                _isDisposed = true;
            }
        }

        // State management
        private WebSocketClientState _state = WebSocketClientState.Disconnected;
        private readonly object _stateLock = new object();

        public WebSocketClientState State
        {
            get
            {
                lock (_stateLock) return _state;
            }
        }

        private void SetState(WebSocketClientState newState, string reason = null)
        {
            lock (_stateLock)
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
