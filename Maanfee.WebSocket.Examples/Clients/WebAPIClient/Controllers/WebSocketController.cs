using Maanfee.WebSocket;
using Microsoft.AspNetCore.Mvc;

namespace WebAPIClient.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebSocketClientController : ControllerBase
    {
        private readonly MaanfeeWebSocketClient _webSocketClient;
        private readonly ILogger<WebSocketClientController> _logger;

        public WebSocketClientController(MaanfeeWebSocketClient webSocketClient, ILogger<WebSocketClientController> logger)
        {
            _webSocketClient = webSocketClient;
            _logger = logger;
        }

        [HttpPost("SendTest")]
        public async Task<IActionResult> SendMessage()
        {
            try
            {
                if (!_webSocketClient.IsConnected)
                {
                    return BadRequest(new { error = "WebSocket is not connected" });
                }

                await _webSocketClient.SendMessageAsync($"Web API Message at {DateTime.Now}");

                //_logger.LogInformation("Message sent via WebSocket: {Message}", request.Message);

                return Ok(new { status = "Message sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message via WebSocket");
                return StatusCode(500, new { error = ex.Message });
            }
        }


        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                if (!_webSocketClient.IsConnected)
                {
                    return BadRequest(new { error = "WebSocket is not connected" });
                }

                await _webSocketClient.SendMessageAsync(request.Message);

                _logger.LogInformation("Message sent via WebSocket: {Message}", request.Message);

                return Ok(new { status = "Message sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message via WebSocket");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                isConnected = _webSocketClient.IsConnected,
                connectionState = _webSocketClient.IsConnected ? "Connected" : "Disconnected"
            });
        }

        [HttpPost("reconnect")]
        public async Task<IActionResult> Reconnect()
        {
            try
            {
                await _webSocketClient.DisconnectAsync();
                await Task.Delay(1000); // کمی تاخیر قبل از اتصال مجدد
                await _webSocketClient.ConnectAsync();

                return Ok(new { status = "Reconnected successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reconnecting WebSocket");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class SendMessageRequest
    {
        public string Message { get; set; }
    }
}
