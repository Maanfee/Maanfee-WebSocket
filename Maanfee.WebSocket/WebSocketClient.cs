using System.Net.WebSockets;
using System.Text;

namespace Maanfee.WebSocket
{
    public class WebSocketClient : IDisposable
    {
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

        public WebSocketClient(string ServerUrl = "127.0.0.1", int Port = 5000)
        {
            _serverUrl = $"ws://{ServerUrl}:{Port}/ws";
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task ConnectAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(WebSocketClient));

            try
            {
                await _webSocket.ConnectAsync(new Uri(_serverUrl), CancellationToken.None);
                OnConnected();
                Console.WriteLine("Connected to server");

                _ = Task.Run(StartReceiving);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                throw;
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(WebSocketClient));

            if (_webSocket.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket is not connected");

            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);

            Console.WriteLine($"Sent: {message}");
        }

        private async Task StartReceiving()
        {
            var buffer = new byte[4096];

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
                        OnConnectionClosed("Closed by server");
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnErrorOccurred(ex);
                    break;
                }
            }
        }

        public async Task DisconnectAsync()
        {
            if (_isDisposed) return;

            _cancellationTokenSource.Cancel();

            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
            }

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
                _cancellationTokenSource?.Cancel();
                _webSocket?.Dispose();
                _cancellationTokenSource?.Dispose();
                _isDisposed = true;
            }
        }
    }
}