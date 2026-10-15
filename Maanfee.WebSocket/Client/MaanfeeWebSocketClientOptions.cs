namespace Maanfee.WebSocket
{
    public class MaanfeeWebSocketClientOptions
    {
        private string _host = "127.0.0.1";
        public string Host
        {
            get => _host;
            set => _host = string.IsNullOrWhiteSpace(value) ?
                throw new ArgumentException("Host cannot be null or empty") : value;
        }

        private int _port = 5000;
        public int Port
        {
            get => _port;
            set => _port = value is < 1 or > 65535 ?
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535") : value;
        }

        private long _maxMessageSize = 10 * 1024 * 1024;
        /// <summary>Maximum size of a single received message in bytes. 0 disables the limit.</summary>
        public long MaxMessageSize
        {
            get => _maxMessageSize;
            set => _maxMessageSize = value < 0
                ? throw new ArgumentOutOfRangeException(nameof(MaxMessageSize), "MaxMessageSize cannot be negative")
                : value;
        }

        private string _path = "/ws";
        /// <summary>URL path of the server endpoint. Must match <see cref="MaanfeeWebSocketServerOptions.Path"/>.</summary>
        public string Path
        {
            get => _path;
            set => _path = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Path cannot be null or empty")
                : (value.StartsWith('/') ? value : "/" + value);
        }

        private int _bufferSize = 4096;
        /// <summary>Size in bytes of the buffer used to read a single frame.</summary>
        public int BufferSize
        {
            get => _bufferSize;
            set => _bufferSize = value < 1
                ? throw new ArgumentOutOfRangeException(nameof(BufferSize), "BufferSize must be positive")
                : value;
        }

        /// <summary>
        /// Retries a failing <c>ConnectAsync</c> up to <see cref="RetryCount"/> times. It does NOT reconnect a connection that was
        /// lost later: the client then moves to Disconnected and the caller decides when to call <c>ConnectAsync</c> again.
        /// </summary>
        public bool AutoRetryConnection { get; set; } = false;

        /// <summary>Number of connect attempts when <see cref="AutoRetryConnection"/> is on.</summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>Wait between connect attempts.</summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    }
}
