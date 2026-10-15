using Maanfee.WebSocket;
using System.Net;

// Console chat server.
//  - A line typed by a client is shown here as "Client: <name>: <text>" and relayed to every OTHER client.
//  - A line typed here is sent to ALL clients as "Server: Console: <text>".
// Relaying is application logic, so it lives in this example and not in the library.

var options = new MaanfeeWebSocketServerOptions { Path = "/ws" };
var connections = new WebSocketConnectionManager();
var sender = new WebSocketMessageSender(connections);
await using var server = new MaanfeeWebSocketServer(options, connections);
server.Start();

async Task SendToAllExceptAsync(string text, string exceptConnectionId = null)
{
    var targets = connections.GetAll().Where(c => c.Id != exceptConnectionId);
    await Task.WhenAll(targets.Select(c => sender.SendAsync(c.Id, text)));
}

server.MaanfeeClientConnected += (_, e) => Console.WriteLine($"[+] Client joined ({connections.Count} connected)");
server.MaanfeeClientDisconnected += (_, e) => Console.WriteLine($"[-] Client left ({connections.Count} connected)");

server.MaanfeeMessageReceived += async (_, message) =>
{
    if (message.PayloadType != WebSocketPayloadType.Text)
        return;

    var line = $"Client: {message.AsText()}";   // the client already prefixed its own name
    Console.WriteLine(line);
    await SendToAllExceptAsync(line, message.ConnectionId);
};

var stopping = false;
var listener = new HttpListener();
listener.Prefixes.Add($"http://localhost:5000{options.Path}/");
listener.Prefixes.Add($"http://127.0.0.1:5000{options.Path}/");
listener.Start();

_ = Task.Run(async () =>
{
    while (!stopping)
    {
        try
        {
            var context = await listener.GetContextAsync();

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            var webSocketContext = await context.AcceptWebSocketAsync(null);
            _ = Task.Run(() => server.HandleWebSocketConnectionAsync(webSocketContext.WebSocket));
        }
        catch when (stopping)
        {
            // listener closed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] {ex.Message}");
        }
    }
});

Console.WriteLine($"Server: Console is listening on ws://localhost:5000{options.Path}");
Console.WriteLine("Type a message to send it to all clients, or 'exit' to stop.");

string input;
while ((input = Console.ReadLine()) != null && !input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(input))
        continue;

    var line = $"Server: Console: {input}";
    Console.WriteLine(line);
    await SendToAllExceptAsync(line);
}

stopping = true;
await server.StopAsync();
listener.Stop();
listener.Close();
Console.WriteLine("Server: Console stopped.");
