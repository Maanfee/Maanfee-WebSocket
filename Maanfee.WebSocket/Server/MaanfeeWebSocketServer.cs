using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
﻿using System.Net.WebSockets;
using WS = System.Net.WebSockets.WebSocket;

namespace Maanfee.WebSocket
{
    public partial class MaanfeeWebSocketServer : MaanfeeWebSocketBase, IMaanfeeWebSocketServer, IDisposable
    {
        public MaanfeeWebSocketServer(MaanfeeWebSocketServerOptions options = null, IWebSocketConnectionManager connectionManager = null, ILogger<MaanfeeWebSocketServer> logger = null) : base(logger)
        {
            Options = options ?? new MaanfeeWebSocketServerOptions();
            MessageReader = new WebSocketMessageReader(Options.BufferSize, Options.MaxMessageSize);
            ConnectionManager = connectionManager ?? new WebSocketConnectionManager();
            Logger.LogInformation("WebSocket Server initialized");
        }

        protected IWebSocketConnectionManager ConnectionManager { get; }

        // One entry per live connection: completes after that connection's cleanup. StopAsync waits on these.
        private readonly ConcurrentDictionary<string, Task> _handlers = new();

        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

        protected MaanfeeWebSocketServerOptions Options { get; }

        protected WebSocketMessageReader MessageReader { get; }

        // Events
        public event EventHandler<MaanfeeWebSocketClientEventArgs> MaanfeeClientConnected;
        public event EventHandler<MaanfeeWebSocketClientEventArgs> MaanfeeClientDisconnected;
        public event EventHandler MaanfeeServerStopped;
        public event EventHandler<WebSocketMessage> MaanfeeMessageReceived;

        public virtual async Task HandleWebSocketConnectionAsync(WS webSocket)
        {
            if (!State.CanAcceptConnections())
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Server not accepting connections", CancellationToken.None);
                return;
            }

            // The token of the run this connection belongs to; a later Start() replaces the CTS.
            var cancellationToken = CancellationTokenSource.Token;

            var connectionId = Guid.NewGuid().ToString();
            var connection = new WebSocketConnection(connectionId, webSocket);

            // بررسی وضعیت WebSocket قبل از اضافه کردن
            if (webSocket.State != WebSocketState.Open)
            {
                Logger.LogWarning($"WebSocket is not open. State: {webSocket.State}");
                return;
            }

            ConnectionManager.Add(connection);

            Logger.LogInformation($"Connection opened: {connectionId}. Total connections: {ConnectionManager.Count}");

            // Trigger ClientConnected event
            OnMaanfeeClientConnected(new MaanfeeWebSocketClientEventArgs
            {
                Connection = connection
            });

            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _handlers[connectionId] = finished.Task;

            try
            {
                // Runs until the connection is gone; includes CleanupConnection (Remove, Dispose, Disconnected event).
                await ReceiveMessagesAsync(connection, cancellationToken);
            }
            finally
            {
                _handlers.TryRemove(connectionId, out _);
                finished.SetResult();
            }
        }

        public void Start()
        {
            if (!State.CanStart())
                throw new InvalidOperationException($"Cannot start server. Current state: {State}");

            SetState(WebSocketServerState.Starting, "Starting WebSocket server");

            try
            {
                // One CTS per run (Start..Stop). The previous run is over: StopAsync cancelled it and
                // every connection handler holds its own token copy, so it is safe to dispose.
                var previous = CancellationTokenSource;
                CancellationTokenSource = new CancellationTokenSource();
                previous?.Dispose();

                SetState(WebSocketServerState.Running, "Server is now running and accepting connections");
                Logger.LogInformation("WebSocket Server started");
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
                var handlers = _handlers.Values.ToArray();

                CancellationTokenSource.Cancel();

                // Ask every connection to close. Each receive loop then ends and cleans its own connection up.
                await Task.WhenAll(ConnectionManager.GetAll().Select(CloseForShutdownAsync));

                // Stopped means: every connection has been removed and its Disconnected event has fired.
                var drained = Task.WhenAll(handlers);
                if (await Task.WhenAny(drained, Task.Delay(StopTimeout)) != drained)
                {
                    Logger.LogWarning($"{_handlers.Count} connection(s) did not finish closing within {StopTimeout.TotalSeconds}s");
                }

                SetState(WebSocketServerState.Stopped, "Server stopped successfully");
                // Trigger ServerStopped event
                OnMaanfeeServerStopped();

                Logger.LogInformation("WebSocket Server stopped");
            }
            catch (Exception ex)
            {
                SetState(WebSocketServerState.Faulted, $"Error while stopping: {ex.Message}");
                throw;
            }
        }

        private async Task CloseForShutdownAsync(WebSocketConnection connection)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopping", timeout.Token);
            }
            catch (Exception ex)
            {
                // Peer did not answer: abort, the receive loop then fails and cleans up.
                Logger.LogWarning($"Closing {connection.Id} failed ({ex.Message}); aborting it");
                connection.Dispose();
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

        protected virtual void OnMaanfeeMessageReceived(WebSocketMessage e)
        {
            MaanfeeMessageReceived?.Invoke(this, e);
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error stopping server during disposal: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    CancellationTokenSource?.Cancel();

                    // Abort what is left; each receive loop then cleans its own connection up.
                    foreach (var connection in ConnectionManager.GetAll())
                    {
                        connection.Dispose();
                    }

                    if (State != WebSocketServerState.Stopped)
                    {
                        SetState(WebSocketServerState.Stopped, "Server disposed");
                    }
                }
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
            Logger.LogInformation($"[SERVER] State changed: {oldState} -> {newState} ({reason})");
            StateChanged?.Invoke(this, new WebSocketStateChangedEventArgs<WebSocketServerState>
            {
                OldState = oldState,
                NewState = newState,
                Reason = reason
            });
        }
    }
}