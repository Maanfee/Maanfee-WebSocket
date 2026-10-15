namespace Maanfee.WebSocket
{
    public interface IWebSocketMessageSender
    {
        /// <returns>false if the connection does not exist or is not open.</returns>
        Task<bool> SendAsync(string connectionId, WebSocketPayloadType payloadType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

        Task<bool> SendAsync(string connectionId, string message, CancellationToken cancellationToken = default);
    }
}
