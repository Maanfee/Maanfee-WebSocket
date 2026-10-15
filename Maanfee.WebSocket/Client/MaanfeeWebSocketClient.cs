using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;

namespace Maanfee.WebSocket
{
    public class MaanfeeWebSocketClient : MaanfeeWebSocketBase, IMaanfeeWebSocketClient, IDisposable
    {
        public MaanfeeWebSocketClient(MaanfeeWebSocketClientOptions options = null, ILogger<MaanfeeWebSocketClient> logger = null) : base(logger)
        {
            Options = options ?? new MaanfeeWebSocketClientOptions();
            MessageReader = new WebSocketMessageReader(Options.BufferSize, Options.MaxMessageSize);
            ServerUrl = $"ws://{Options.Host}:{Options.Port}{Options.Path}";

            // Initialize state
            SetState(WebSocketClientState.Disconnected, "Initialized");
        }

        private MaanfeeWebSocketClientOptions Options;
        private readonly WebSocketMessageReader MessageReader;
        private readonly string ServerUrl;

        // Events
        public event EventHandler<string> MessageReceived;
        public event EventHandler<WebSocketMessage> RawMessageReceived;
        public event EventHandler<string> ConnectionClosed;
        public event EventHandler<Exception> ErrorOccurred;
        public event EventHandler Connected;

        /// <summary>
        /// Everything that belongs to one connect/disconnect cycle. A ClientWebSocket and its
        /// CancellationTokenSource are single-use, so every connection gets a fresh session.
        /// </summary>
        private sealed class Session : IDisposable
        {
            public ClientWebSocket Socket { get; } = new ClientWebSocket();
            public CancellationTokenSource Cts { get; }
            // WebSocket allows only one outstanding SendAsync at a time.
            public SemaphoreSlim SendLock { get; } = new SemaphoreSlim(1, 1);
            public Task ReceiveTask { get; set; } = Task.CompletedTask;
            public volatile bool IsClosing;

            public Session(CancellationToken lifetime)
            {
                Cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            }

            public void Dispose()
            {
                Cts.Cancel();
                Socket.Dispose();
                Cts.Dispose();
            }
        }

        // Cancelled on dispose: aborts a connect or retry delay that is still in flight.
        private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();

        private volatile Session _session;

        public bool IsConnected => _session?.Socket.State == WebSocketState.Open &&
                   State == WebSocketClientState.Connected;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MaanfeeWebSocketClient));

            lock (StateLock)
            {
                if (!State.CanConnect())
                    throw new InvalidOperationException($"Cannot connect. Current state: {State}");

                SetState(WebSocketClientState.Connecting, "Starting connection process");
            }

            // The previous session (if any) is finished: its receive loop ended and moved the state to a terminal one.
            Interlocked.Exchange(ref _session, null)?.Dispose();

            try
            {
                await ConnectWithRetryAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Caller gave up (or the client is being disposed): no session, nothing connected.
                SetState(WebSocketClientState.Disconnected, "Connect cancelled");
                throw;
            }
        }

        private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
        {
            if (Options.AutoRetryConnection)
            {
                for (int i = 0; i < Options.RetryCount; i++)
                {
                    try
                    {
                        await InternalConnectAsync(cancellationToken);
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
                        // Cancelled by the caller's token or by Dispose; either way the retry loop ends here.
                        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
                        await Task.Delay(Options.RetryDelay, delayCts.Token);
                    }
                }
            }
            else
            {
                try
                {
                    await InternalConnectAsync(cancellationToken);
                }
                catch (MaanfeeWebSocketException ex)
                {
                    SetState(WebSocketClientState.Faulted, $"Connection failed: {ex.Message}");
                    OnErrorOccurred(ex);
                    throw;
                }
            }
        }

        private async Task InternalConnectAsync(CancellationToken cancellationToken)
        {
            // New ClientWebSocket + CancellationTokenSource for every attempt.
            var session = new Session(_disposeCts.Token);

            // The caller's token only covers the handshake; the session must outlive it.
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(session.Cts.Token, cancellationToken);

            try
            {
                await session.Socket.ConnectAsync(new Uri(ServerUrl), connectCts.Token);

                if (session.Socket.State != WebSocketState.Open)
                {
                    throw new MaanfeeWebSocketException($"WebSocket connection failed. State: {session.Socket.State}");
                }
            }
            catch (Exception) when (connectCts.IsCancellationRequested)
            {
                session.Dispose();
                throw new OperationCanceledException(connectCts.Token);
            }
            catch (Exception ex)
            {
                session.Dispose();

                if (ex is MaanfeeWebSocketException)
                {
                    OnErrorOccurred(ex);
                    throw;
                }

                var maanfeeEx = new MaanfeeWebSocketException($"Connection failed: {ex.Message}", innerException: ex);
                OnErrorOccurred(maanfeeEx);
                throw maanfeeEx;
            }

            if (_isDisposed)
            {
                session.Dispose();
                throw new ObjectDisposedException(nameof(MaanfeeWebSocketClient));
            }

            _session = session;
            SetState(WebSocketClientState.Connected, "Successfully connected to server");
            OnConnected();
            Logger.LogInformation("Connected to server");

            // Async I/O, no ThreadPool hop needed. Started last so no message can be raised before Connected.
            session.ReceiveTask = ReceiveLoopAsync(session);
        }

        public async Task SendAsync(WebSocketPayloadType payloadType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new MaanfeeWebSocketException($"{nameof(MaanfeeWebSocketClient)} is disposed");

            var session = _session;
            if (!State.CanSend() || session == null)
                throw new MaanfeeWebSocketException($"Cannot send message. Current state: {State}");

            var messageType = payloadType == WebSocketPayloadType.Text ? WebSocketMessageType.Text : WebSocketMessageType.Binary;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(session.Cts.Token, cancellationToken);

            await session.SendLock.WaitAsync(linked.Token);
            try
            {
                await session.Socket.SendAsync(payload, messageType, true, linked.Token);
            }
            finally
            {
                session.SendLock.Release();
            }

            Logger.LogDebug($"Sent {payloadType} message ({payload.Length} bytes)");
        }

        public Task SendTextAsync(string message, CancellationToken cancellationToken = default) =>
            SendAsync(WebSocketPayloadType.Text, Encoding.UTF8.GetBytes(message), cancellationToken);

        public Task SendBinaryAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default) =>
            SendAsync(WebSocketPayloadType.Binary, payload, cancellationToken);

        private async Task ReceiveLoopAsync(Session session)
        {
            // Only the loop of the current session may change the client state, and not while we are closing on purpose.
            bool OwnsState() => ReferenceEquals(_session, session) && !session.IsClosing;

            try
            {
                while (!session.Cts.Token.IsCancellationRequested)
                {
                    var message = await MessageReader.ReadAsync(ServerUrl, (buffer, ct) => session.Socket.ReceiveAsync(buffer, ct), session.Cts.Token);

                    if (message == null)
                    {
                        await AcknowledgeCloseAsync(session);

                        if (OwnsState())
                        {
                            SetState(WebSocketClientState.Disconnected, "Server closed connection");
                            OnConnectionClosed("Closed by server");
                        }
                        break;
                    }

                    RawMessageReceived?.Invoke(this, message);

                    if (message.PayloadType == WebSocketPayloadType.Text)
                    {
                        OnMessageReceived(message.AsText());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (OwnsState())
                    SetState(WebSocketClientState.Disconnected, "Operation cancelled");
            }
            // The peer went away. Besides ConnectionClosedPrematurely, browsers (Blazor WebAssembly) report a close that
            // arrives while a receive is pending as a WebSocketException (NativeError) with the socket no longer open.
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely
                                                || session.Socket.State is WebSocketState.Closed or WebSocketState.Aborted or WebSocketState.CloseReceived)
            {
                if (OwnsState())
                {
                    SetState(WebSocketClientState.Disconnected, "Connection closed prematurely");
                    OnConnectionClosed("Connection closed");
                }
            }
            catch (MaanfeeWebSocketException ex)
            {
                if (OwnsState())
                {
                    SetState(WebSocketClientState.Faulted, $"Error receiving: {ex.Message}");
                    OnErrorOccurred(ex);
                }
            }
            catch (Exception ex)
            {
                if (OwnsState())
                {
                    SetState(WebSocketClientState.Faulted, $"Unexpected error: {ex.Message}");
                    OnErrorOccurred(ex);
                }
            }
        }

        // Completes the close handshake when the server initiated it.
        private async Task AcknowledgeCloseAsync(Session session)
        {
            if (session.Socket.State != WebSocketState.CloseReceived)
                return;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await session.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledging close", timeout.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
            {
                Logger.LogDebug($"Close acknowledgement failed: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (_isDisposed)
                return;

            var session = Interlocked.Exchange(ref _session, null);
            if (session == null)
                return;

            // The connection already ended on its own (server closed it, network lost): only release what is left.
            if (State is WebSocketClientState.Disconnected or WebSocketClientState.Faulted)
            {
                await CloseSessionAsync(session);
                return;
            }

            SetState(WebSocketClientState.Disconnecting, "Client initiated disconnect");

            await CloseSessionAsync(session);

            SetState(WebSocketClientState.Disconnected, "Successfully disconnected");
            OnConnectionClosed("Disconnected by client");
            Logger.LogInformation("Disconnected from server");
        }

        /// <summary>Closes and disposes one session: graceful close, receive loop finished, resources released. No state changes, no events.</summary>
        private async Task CloseSessionAsync(Session session)
        {
            session.IsClosing = true;

            try
            {
                // Graceful close first: the server answers with a close frame and the receive loop ends by itself.
                if (session.Socket.State == WebSocketState.Open)
                {
                    using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        await session.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", closeTimeout.Token);
                    }
                    catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
                    {
                        Logger.LogWarning($"Graceful close failed: {ex.Message}");
                    }
                }

                // Make sure the loop is gone before the session is thrown away.
                session.Cts.Cancel();
                await Task.WhenAny(session.ReceiveTask, Task.Delay(TimeSpan.FromSeconds(5)));
            }
            finally
            {
                session.Dispose();
            }
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

        protected override async ValueTask DisposeAsyncCore()
        {
            // Decided by the session, not by the state: Connecting, Reconnecting, Disconnecting and Faulted
            // can all still own a session (a finished receive loop leaves it behind until the next connect).
            var session = Interlocked.Exchange(ref _session, null);
            if (session == null)
                return;

            try
            {
                await CloseSessionAsync(session).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error closing session during disposal: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Aborts a connect or retry delay still in flight, then releases what is left.
                    _disposeCts.Cancel();
                    Interlocked.Exchange(ref _session, null)?.Dispose();
                    SetState(WebSocketClientState.Disposed, "Client disposed");
                    _disposeCts.Dispose();
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
            Logger.LogInformation($"[CLIENT] State changed: {oldState} -> {newState} ({reason})");
            StateChanged?.Invoke(this, new WebSocketStateChangedEventArgs<WebSocketClientState>
            {
                OldState = oldState,
                NewState = newState,
                Reason = reason
            });
        }

    }
}
