using Maanfee.WebSocket;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// ********************************
// 🔥 WebSocket Middleware - این قسمت مهم است
app.UseWebSockets(); // حتما این خط باید باشد

var webSocketServer = app.Services.GetRequiredService<WebSocketServer>();
webSocketServer.Start();

// ثبت event handlers برای سرور
webSocketServer.ClientConnected += (sender, e) =>
{
    Console.WriteLine($"[SERVER] ✅ Client connected: {e.ClientId}");
};

webSocketServer.ClientDisconnected += (sender, e) =>
{
    Console.WriteLine($"[SERVER] ❌ Client disconnected: {e.ClientId}");
};

webSocketServer.MessageReceived += (sender, e) =>
{
    Console.WriteLine($"[SERVER] 📩 Received from {e.ClientId}: {e.Message}");
};

// WebSocket endpoint middleware
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await webSocketServer.HandleWebSocketConnection(webSocket);
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

app.Run();
