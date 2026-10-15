using Microsoft.AspNetCore.Builder;

namespace Maanfee.WebSocket
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Accepts WebSocket requests on <see cref="MaanfeeWebSocketServerOptions.Path"/> and hands them to <see cref="MaanfeeWebSocketServer"/>.
        /// Requires <c>AddMaanfeeWebSocket()</c>.
        /// </summary>
        public static IApplicationBuilder UseWebSocketServer(this IApplicationBuilder app)
        {
            app.UseWebSockets();
            return app.UseMiddleware<WebSocketServerMiddleware>();
        }
    }
}
