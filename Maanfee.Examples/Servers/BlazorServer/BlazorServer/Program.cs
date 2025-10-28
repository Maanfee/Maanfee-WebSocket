using BlazorServer.Components;
using Maanfee.WebSocket;
using MudBlazor.Services;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();

// ********************************
builder.Services.AddSingleton<WebSocketServer>();

// اضافه کردن CORS برای اجازه دسترسی به API کلاینت
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
// ********************************

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

// ********************************
// 🔥 WebSocket Middleware
app.UseWebSockets(); // حتما این خط باید باشد

var webSocketServer = app.Services.GetRequiredService<WebSocketServer>();
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
                // این خطا طبیعی است وقتی کلاینت connection را می‌بندد
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
// ********************************

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BlazorServer.Client._Imports).Assembly);

app.Run();
