using Maanfee.WebSocket;

// Console chat client.
//  - What you type is sent to the server, which shows it as "Client: Console: <text>" and relays it to the other clients.
//  - Messages from the server and from other clients are printed as they arrive.

const string Name = "Console";

await using var client = new MaanfeeWebSocketClient(new MaanfeeWebSocketClientOptions
{
    Host = "127.0.0.1",
    Port = 5000,
    Path = "/ws",
});

client.MessageReceived += (_, text) => Console.WriteLine(text);
client.ConnectionClosed += (_, reason) => Console.WriteLine($"[connection closed: {reason}]");
client.ErrorOccurred += (_, ex) => Console.WriteLine($"[error: {ex.Message}]");

try
{
    await client.ConnectAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Could not connect: {ex.Message}");
    return;
}

Console.WriteLine("Client: Console connected. Type a message, or 'exit' to quit.");

string input;
while ((input = Console.ReadLine()) != null && !input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(input))
        continue;

    if (!client.IsConnected)
    {
        Console.WriteLine("[not connected]");
        break;
    }

    
    await client.SendTextAsync($"{Name}: {input}");
}

await client.DisconnectAsync();
