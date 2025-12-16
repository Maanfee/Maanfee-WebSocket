using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

namespace Maanfee.WebSocket
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseMaanfeeWebSocket(this IApplicationBuilder app, Action<MaanfeeWebSocketServer> configureEvents = null)
        {
            // 🔥 WebSocket Middleware
            app.UseWebSockets(); // حتما این خط باید باشد

            var webSocketServer = app.ApplicationServices.GetRequiredService<MaanfeeWebSocketServer>();

            // Subscribe to state changes
            webSocketServer.StateChanged += (sender, e) =>
            {
                Console.WriteLine($"[SERVER STATE] {e.OldState} -> {e.NewState} ({e.Reason})");

                // Log to different levels based on severity
                if (e.NewState == WebSocketServerState.Faulted)
                {
                    Console.WriteLine($"[ERROR] Server entered faulted state: {e.Reason}");
                }
            };

            // ثبت event handlers برای سرور
            webSocketServer.MaanfeeClientConnected += (sender, e) =>
            {
                Console.WriteLine($"[SERVER] ✅ Client connected: {e.User.Id}");
            };

            webSocketServer.MaanfeeClientDisconnected += (sender, e) =>
            {
                Console.WriteLine($"[SERVER] ❌ Client disconnected: {e.User.Id}");
            };

            webSocketServer.MaanfeeMessageReceived += (sender, e) =>
            {
                Console.WriteLine($"[SERVER] 📩 Received from {e.User.Id}: {e.Message}");
            };

            // event های سفارشی کاربر را اضافه کنید
            configureEvents?.Invoke(webSocketServer);

            webSocketServer.Start();
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
                        // Check server state
                        if (!webSocketServer.State.CanAcceptConnections())
                        {
                            context.Response.StatusCode = 503; // Service Unavailable
                            await context.Response.WriteAsync("WebSocket server is not ready");
                            return;
                        }

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

            return app;
        }
    }
}