namespace Maanfee.WebSocket
{
    public class MaanfeeWebSocketOption
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

        public bool AutoRetryConnection { get; set; } = false;

        public int RetryCount { get; set; } = 3;

        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    }
}
