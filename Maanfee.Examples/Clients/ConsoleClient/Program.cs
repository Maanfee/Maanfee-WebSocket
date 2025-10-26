using Maanfee.WebSocket;

class Program
{
    private static WebSocketClient client;

    static async Task Main(string[] args)
    {
        //client = new WebSocketClient("ws://localhost:5000/");
        client = new WebSocketClient("127.0.0.1", 5000);

        // ثبت event handlers
        client.Connected += OnConnected;
        client.MessageReceived += OnMessageReceived;
        client.ConnectionClosed += OnConnectionClosed;
        client.ErrorOccurred += OnErrorOccurred;

        try
        {
            await client.ConnectAsync();

            Console.WriteLine("📝 Type messages to send (type 'exit' to quit):");

            // حلقه ارسال پیام
            while (true)
            {
                var message = Console.ReadLine();

                if (message?.ToLower() == "exit")
                    break;

                if (!string.IsNullOrWhiteSpace(message))
                {
                    await client.SendMessageAsync(message);
                }
            }

            await client.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
        finally
        {
            client.Dispose();
        }
    }

    private static void OnConnected(object sender, EventArgs e)
    {
        Console.WriteLine("✅ Connected to server!");
    }

    private static void OnMessageReceived(object sender, string message)
    {
        Console.WriteLine($"📨 Received: {message}");
    }

    private static void OnConnectionClosed(object sender, string reason)
    {
        Console.WriteLine($"🔌 Connection closed: {reason}");
    }

    private static void OnErrorOccurred(object sender, Exception exception)
    {
        Console.WriteLine($"💥 Error: {exception.Message}");
    }
}