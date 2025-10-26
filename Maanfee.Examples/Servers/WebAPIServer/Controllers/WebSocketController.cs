using Maanfee.WebSocket;
using Microsoft.AspNetCore.Mvc;

namespace WebAPIServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebSocketController : ControllerBase
    {
        public WebSocketController(WebSocketServer webSocketServer)
        {
            _webSocketServer = webSocketServer;
        }

        private readonly WebSocketServer _webSocketServer;

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                connectedClients = _webSocketServer.GetConnectedClientsCount(),
                isRunning = true // می‌توانید این property را به سرور اضافه کنید
            });
        }

        [HttpPost("broadcast")]
        public async Task<IActionResult> BroadcastMessage([FromBody] string message)
        {
            await _webSocketServer.SendToAllAsync(message);
            return Ok(new { status = "Message broadcasted to all clients" });
        }

        [HttpPost("stop")]
        public async Task<IActionResult> StopServer()
        {
            await _webSocketServer.StopAsync();
            return Ok(new { status = "Server stopped" });
        }
    }
}