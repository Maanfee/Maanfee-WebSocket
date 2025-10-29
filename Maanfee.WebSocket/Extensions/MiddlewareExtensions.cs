using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

namespace Maanfee.WebSocket
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseMaanfeeWebSocket(this IApplicationBuilder app, Action<WebSocketServer> configureEvents = null)
        {
            // 🔥 WebSocket Middleware
            app.UseWebSockets(); // حتما این خط باید باشد

            var webSocketServer = app.ApplicationServices.GetRequiredService<WebSocketServer>();
            webSocketServer.Start();

            // ثبت event handlers برای سرور
            webSocketServer.ClientConnected += (sender, e) =>
            {
                Console.WriteLine($"[SERVER] ✅ Client connected: {e.User.Id}");
            };

            webSocketServer.ClientDisconnected += (sender, e) =>
            {
                Console.WriteLine($"[SERVER] ❌ Client disconnected: {e.User.Id}");
            };

            webSocketServer.MessageReceived += (sender, e) =>
            {
                Console.WriteLine($"[SERVER] 📩 Received from {e.User.Id}: {e.Message}");
            };

            // ثبت event handlers سفارشی
            // اگر مستقیم در سرور اجرا شود این خط را می توام حذف نمود
            configureEvents?.Invoke(webSocketServer);
            /*
            app.UseMaanfeeWebSocket(webSocketServer =>
            {
                webSocketServer.ClientConnected += (sender, e) =>
                {
                    Console.WriteLine($"[CUSTOM] 🎉 New client: {e.User.Id}");
                };
            });
            */

            // WebSocket endpoint middleware
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        try
                        {
                            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                            await webSocketServer.HandleWebSocketConnectionAsync(webSocket);
                        }
                        catch (System.Net.WebSockets.WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                        {
                            // این خطا طبیعی است وقتی کلاینت کانکشن را می‌بندد
                            Console.WriteLine($"WebSocket connection closed prematurely: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"WebSocket error: {ex.Message}");
                            context.Response.StatusCode = 500;
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }
            });

            // استفاده از middleware
            return app;
        }
    }
}