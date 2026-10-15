#nullable enable
using System.Buffers;
using System.Net.WebSockets;

namespace Maanfee.WebSocket
{
    /// <summary>
    /// Reassembles fragmented WebSocket frames into complete messages.
    /// </summary>
    public sealed class WebSocketMessageReader
    {
        private readonly int _bufferSize;
        private readonly long _maxMessageSize;

        public WebSocketMessageReader(int bufferSize = 4096, long maxMessageSize = 0)
        {
            _bufferSize = bufferSize;
            _maxMessageSize = maxMessageSize;
        }

        /// <summary>
        /// Reads frames until a complete message arrives.
        /// Returns null when the remote side sent a close frame.
        /// </summary>
        /// <exception cref="MaanfeeWebSocketException">The message exceeds the maximum size (CloseStatus = MessageTooBig).</exception>
        public Task<WebSocketMessage?> ReadAsync(WebSocketConnection connection, CancellationToken cancellationToken) =>
            ReadAsync(connection.Id, connection.ReceiveAsync, cancellationToken);

        /// <summary>Same as above for any source of frames (e.g. a ClientWebSocket).</summary>
        public async Task<WebSocketMessage?> ReadAsync(string sourceId, Func<ArraySegment<byte>, CancellationToken, Task<WebSocketReceiveResult>> receiveFrameAsync, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);

            try
            {
                using var message = new MemoryStream(_bufferSize);

                while (true)
                {
                    var frame = await receiveFrameAsync(new ArraySegment<byte>(buffer, 0, _bufferSize), cancellationToken);

                    if (frame.MessageType == WebSocketMessageType.Close)
                        return null;

                    message.Write(buffer, 0, frame.Count);

                    if (_maxMessageSize > 0 && message.Length > _maxMessageSize)
                    {
                        throw new MaanfeeWebSocketException(
                            $"Message from {sourceId} exceeds max size ({message.Length} > {_maxMessageSize})",
                            WebSocketCloseStatus.MessageTooBig);
                    }

                    if (frame.EndOfMessage)
                    {
                        var payloadType = frame.MessageType == WebSocketMessageType.Text
                            ? WebSocketPayloadType.Text
                            : WebSocketPayloadType.Binary;

                        return new WebSocketMessage(sourceId, payloadType, message.ToArray());
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
