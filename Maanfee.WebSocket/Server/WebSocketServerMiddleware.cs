using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;

namespace Maanfee.WebSocket
{
    /// <summary>Routes WebSocket upgrade requests to the server. Nothing else.</summary>
    public sealed class WebSocketServerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly MaanfeeWebSocketServer _server;
        private readonly MaanfeeWebSocketServerOptions _options;
        private readonly ILogger<WebSocketServerMiddleware> _logger;

        public WebSocketServerMiddleware(RequestDelegate next, MaanfeeWebSocketServer server, MaanfeeWebSocketServerOptions options, ILogger<WebSocketServerMiddleware> logger)
        {
            _next = next;
            _server = server;
            _options = options;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.Equals(_options.Path, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (!_server.State.CanAcceptConnections())
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("WebSocket server is not ready");
                return;
            }

            try
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await _server.HandleWebSocketConnectionAsync(webSocket);
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                // Normal when the client drops the connection.
                _logger.LogDebug($"WebSocket connection closed prematurely: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket error");
                if (!context.Response.HasStarted)
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }
    }
}
