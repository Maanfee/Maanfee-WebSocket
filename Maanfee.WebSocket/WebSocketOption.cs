namespace Maanfee.WebSocket
{
    public class WebSocketOption
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

        private int _bufferSize = 4096;
        public int BufferSize
        {
            get => _bufferSize;
            set => _bufferSize = value < 1024 ?
                throw new ArgumentOutOfRangeException(nameof(BufferSize), "Buffer size must be at least 1024 bytes") : value;
        }

        public bool AutoRetryConnection { get; set; } = false;

        public int RetryCount { get; set; } = 3;

        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    }
}
