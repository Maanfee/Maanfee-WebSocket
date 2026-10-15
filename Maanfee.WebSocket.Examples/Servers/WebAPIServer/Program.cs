using Maanfee.WebSocket;
using WebAPIServer;

var builder = WebApplication.CreateBuilder(args);

// The WebSocket server, its connection manager, the message sender and a hosted service
// that starts the server with the host and stops it gracefully on shutdown.
builder.Services.AddMaanfeeWebSocket(options => options.Path = "/ws");

// The chat behaviour of this example (relay between clients, server messages).
builder.Services.AddSingleton<ChatRelay>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ChatRelay>());

var app = builder.Build();

// Clients connect to ws://localhost:5000/ws
app.UseWebSocketServer();

var chat = app.MapGroup("/api/chat");

// POST /api/chat/send   body: { "message": "hello" }  -> every client receives "Server: WebAPI: hello"
chat.MapPost("/send", async (SendRequest request, ChatRelay relay) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "message is required" });

    var recipients = relay.ConnectedCount;
    await relay.SendToAllAsync(request.Message);
    return Results.Ok(new { status = "sent", recipients });
});

// The chat log kept by the server (client lines and server lines).
chat.MapGet("/messages", (ChatRelay relay) => relay.Log);

chat.MapGet("/status", (ChatRelay relay, MaanfeeWebSocketServer server) => new
{
    state = server.State.ToString(),
    connectedClients = relay.ConnectedCount,
});

app.Run();

public record SendRequest(string Message);
