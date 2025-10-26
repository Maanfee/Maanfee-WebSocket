using Maanfee.WebSocket;
using System.Net;

var server = new WebSocketServer();
server.Start();

// ثبت event handlers برای سرور
server.ClientConnected += (sender, e) =>
{
    Console.WriteLine($"[SERVER] ✅ Client connected: {e.ClientId} at {e.ConnectedTime:T}");
};

server.ClientDisconnected += (sender, e) =>
{
    Console.WriteLine($"[SERVER] ❌ Client disconnected: {e.ClientId} at {e.DisconnectedTime:T}");
};

server.MessageReceived += (sender, e) =>
{
    Console.WriteLine($"[SERVER] 📩 Received from {e.ClientId}: {e.Message}");
};

server.ServerStopped += (sender, e) =>
{
    Console.WriteLine("[SERVER] 🛑 Server stopped");
};

// راه‌اندازی HTTP Listener برای WebSocket
var httpListener = new HttpListener();
httpListener.Prefixes.Add("http://localhost:5000/ws/"); // اضافه کردن /ws/
httpListener.Prefixes.Add("http://127.0.0.1:5000/ws/"); // برای اتصال از localhost و 127.0.0.1
httpListener.Start();

Console.WriteLine("🚀 WebSocket Server started on http://localhost:5000/");
Console.WriteLine("Press 'q' to stop server...");

// پردازش درخواست‌های WebSocket - این قسمت اصلاح شده
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            var context = await httpListener.GetContextAsync();

            if (context.Request.IsWebSocketRequest)
            {
                var webSocketContext = await context.AcceptWebSocketAsync(null);

                // هر اتصال جدید در یک Task جداگانه اجرا شود
                _ = Task.Run(async () =>
                {
                    await server.HandleWebSocketConnection(webSocketContext.WebSocket);
                });
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                Console.WriteLine($"[SERVER] Rejected non-WebSocket request from {context.Request.RemoteEndPoint}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SERVER] Error: {ex.Message}");
        }
    }
});

// منتظر فشار دادن کلید 'q' برای توقف سرور
while (Console.ReadKey().Key != ConsoleKey.Q)
{
    Console.WriteLine("\nPress 'q' to stop server...");
}

await server.StopAsync();
httpListener.Stop();
httpListener.Close();
Console.WriteLine("👋 Server shutdown completed");