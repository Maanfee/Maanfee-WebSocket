using Maanfee.WebSocket;

namespace WebAPIClient.Services
{
    public class WebSocketClientService : BackgroundService
    {
        private readonly WebSocketClient _webSocketClient;
        private readonly ILogger<WebSocketClientService> _logger;

        public WebSocketClientService(WebSocketClient webSocketClient, ILogger<WebSocketClientService> logger)
        {
            _webSocketClient = webSocketClient;
            _logger = logger;

            // ثبت event handlers
            _webSocketClient.MessageReceived += OnMessageReceived;
            _webSocketClient.ConnectionClosed += OnConnectionClosed;
            _webSocketClient.ErrorOccurred += OnErrorOccurred;
            _webSocketClient.Connected += OnConnected;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WebSocket Client Service starting...");

            try
            {
                await _webSocketClient.ConnectAsync();
                _logger.LogInformation("WebSocket Client connected to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to WebSocket server");
            }

            // منتظر ماندن تا سرویس متوقف شود
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogInformation("WebSocket Client Service stopping...");
            await _webSocketClient.DisconnectAsync();
        }

        private void OnMessageReceived(object sender, string message)
        {
            _logger.LogInformation("Received message from server: {Message}", message);

            // در اینجا می‌توانید پیام را پردازش کنید
            // مثلاً ذخیره در دیتابیس، ارسال به سرویس دیگر و غیره
            ProcessReceivedMessage(message);
        }

        private void OnConnected(object sender, EventArgs e)
        {
            _logger.LogInformation("WebSocket connection established");
        }

        private void OnConnectionClosed(object sender, string reason)
        {
            _logger.LogWarning("WebSocket connection closed: {Reason}", reason);
        }

        private void OnErrorOccurred(object sender, Exception exception)
        {
            _logger.LogError(exception, "WebSocket error occurred");
        }

        private void ProcessReceivedMessage(string message)
        {
            // پردازش پیام دریافتی
            // مثلاً:
            // - ذخیره در دیتابیس
            // - ارسال notification
            // - پردازش business logic
            _logger.LogInformation("Processing message: {Message}", message);
        }

        public override void Dispose()
        {
            _webSocketClient.MessageReceived -= OnMessageReceived;
            _webSocketClient.ConnectionClosed -= OnConnectionClosed;
            _webSocketClient.ErrorOccurred -= OnErrorOccurred;
            _webSocketClient.Connected -= OnConnected;

            _webSocketClient?.Dispose();
            base.Dispose();
        }
    }
}