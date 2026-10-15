using Microsoft.Extensions.Logging;
﻿using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace Maanfee.WebSocket
{
    public partial class MaanfeeWebSocketServer
    {

        protected virtual async Task ReceiveMessagesAsync(WebSocketConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                while (connection.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var message = await MessageReader.ReadAsync(connection, cancellationToken);

                    if (message == null)
                    {
                        Logger.LogDebug($"🔌 Close message received from {connection.Id}");
                        break;
                    }

                    Logger.LogDebug($"📨 Received {message.Payload.Length} {message.PayloadType} bytes from {connection.Id}");

                    OnMaanfeeMessageReceived(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Server is stopping - this is normal
                Logger.LogInformation($"⏹️ Operation canceled for connection {connection.Id}");
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                // Remote side closed the connection unexpectedly
                Logger.LogWarning($"⚠️ Connection {connection.Id} closed connection prematurely: {ex.Message}");
            }
            catch (MaanfeeWebSocketException ex)
            {
                Logger.LogWarning($"⚠️ {ex.Message}");

                if (ex.CloseStatus is { } closeStatus && connection.State == WebSocketState.Open)
                {
                    await connection.CloseAsync(closeStatus, ex.Message, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"💥 Unexpected error for connection {connection.Id}: {ex.Message}");
            }
            finally
            {
                await CleanupConnection(connection);
            }
        }

        protected virtual async Task CleanupConnection(WebSocketConnection connection)
        {
            try
            {
                connection.MarkDisconnected();

                ConnectionManager.Remove(connection.Id);

                if (connection.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    try
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", timeout.Token);
                    }
                    catch (Exception ex) when (ex is MaanfeeWebSocketException or ObjectDisposedException or WebSocketException or InvalidOperationException or OperationCanceledException)
                    {
                        // Already closed, disposed or the peer did not answer: nothing left to do during cleanup
                        Logger.LogDebug($"Close during cleanup of {connection.Id} ended with {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during connection cleanup {connection.Id}: {ex.Message}");
            }
            finally
            {
                try
                {
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error disposing websocket for {connection.Id}: {ex.Message}");
                }

                Logger.LogInformation($"Connection closed: {connection.Id}. Total connections: {ConnectionManager.Count}");
                OnMaanfeeClientDisconnected(new MaanfeeWebSocketClientEventArgs
                {
                    Connection = connection
                });
            }
        }


    }
}
