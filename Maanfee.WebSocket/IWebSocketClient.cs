namespace Maanfee.WebSocket
{
    public interface IWebSocketClient
    {
        Task ConnectAsync();

        Task SendMessageAsync(string message);

        Task DisconnectAsync();

        void Dispose();

        // Events
        event EventHandler<string> MessageReceived;
        event EventHandler<string> ConnectionClosed;
        event EventHandler<Exception> ErrorOccurred;
        event EventHandler Connected;
    }
}
