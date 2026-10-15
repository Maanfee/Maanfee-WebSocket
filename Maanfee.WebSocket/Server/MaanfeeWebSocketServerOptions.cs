namespace Maanfee.WebSocket
{
    public class MaanfeeWebSocketServerOptions
    {
        private string _path = "/ws";
        /// <summary>URL path that accepts WebSocket connections. Clients must use the same path.</summary>
        public string Path
        {
            get => _path;
            set => _path = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Path cannot be null or empty")
                : (value.StartsWith('/') ? value : "/" + value);
        }

        private long _maxMessageSize = 10 * 1024 * 1024;
        /// <summary>Maximum size of a single message in bytes. 0 disables the limit.</summary>
        public long MaxMessageSize
        {
            get => _maxMessageSize;
            set => _maxMessageSize = value < 0
                ? throw new ArgumentOutOfRangeException(nameof(MaxMessageSize), "MaxMessageSize cannot be negative")
                : value;
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
    }
}
