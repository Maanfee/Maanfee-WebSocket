using WebAPIClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ChatClientService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ChatClientService>());

var app = builder.Build();

var chat = app.MapGroup("/api/chat");

// What the Web API client received so far (messages from the server and from other clients).
chat.MapGet("/messages", (ChatClientService service) => service.Messages);

chat.MapGet("/status", (ChatClientService service) => new
{
    isConnected = service.IsConnected,
    state = service.State.ToString(),
});

// POST /api/chat/send   body: { "message": "hello" }
chat.MapPost("/send", async (SendRequest request, ChatClientService service, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "message is required" });

    if (!service.IsConnected)
        return Results.Conflict(new { error = "not connected to the server" });

    await service.SendAsync(request.Message, cancellationToken);
    return Results.Ok(new { status = "sent" });
});

chat.MapPost("/reconnect", async (ChatClientService service, CancellationToken cancellationToken) =>
{
    await service.ReconnectAsync(cancellationToken);
    return Results.Ok(new { status = "connected" });
});

app.Run();

public record SendRequest(string Message);
