using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace Maanfee.WebSocket
{
    public partial class MaanfeeWebSocketServer
    {
        protected readonly List<MaanfeeWebSocketUser> Users = new List<MaanfeeWebSocketUser>();

        public int GetConnectedUsersCount()
        {
            lock (_lock)
            {
                return Users.Count(u => u.IsConnected);
            }
        }

        public List<string> GetConnectedUserIds()
        {
            lock (_lock)
            {
                return Users.Where(u => u.IsConnected).Select(u => u.Id).ToList();
            }
        }

        public MaanfeeWebSocketUser GetUserById(string userId)
        {
            lock (_lock)
            {
                return Users.FirstOrDefault(u => u.Id == userId);
            }
        }

        public List<MaanfeeWebSocketUser> GetAllUsers()
        {
            lock (_lock)
            {
                return Users.ToList();
            }
        }

        // **********************************

        protected virtual async Task ReceiveMessageFromClientAsync(MaanfeeWebSocketUser user)
        {
            // استفاده از ArrayPool برای مدیریت بهتر حافظه
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);

            // 🔴 اصلاح: استفاده از MemoryStream با ظرفیت اولیه مناسب
            using var messageBuilder = new MemoryStream(DefaultBufferSize);

            try
            {
                while (user.IsConnected && !CancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await user.WebSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer, 0, DefaultBufferSize),
                        CancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // 🔥 نوشتن chunk به MemoryStream
                        await messageBuilder.WriteAsync(buffer.AsMemory(0, result.Count), CancellationTokenSource.Token);

                        // 🔥 بررسی اندازه پیام (اگر تنظیمات داشته باشیم)
                        if (MaxMessageSize > 0 && messageBuilder.Length > MaxMessageSize)
                        {
                            Console.WriteLine($"⚠️ Message from {user.Id} exceeds max size ({messageBuilder.Length} > {MaxMessageSize})");
                            await user.WebSocket.CloseAsync(
                                WebSocketCloseStatus.MessageTooBig,
                                "Message too large",
                                CancellationToken.None);
                            break;
                        }

                        if (result.EndOfMessage)
                        {
                            // 🔥 تبدیل کل محتوا به string
                            string fullMessage;
                            if (messageBuilder.TryGetBuffer(out var segment))
                            {
                                fullMessage = Encoding.UTF8.GetString(segment.Array,
                                    segment.Offset, segment.Count);
                            }
                            else
                            {
                                messageBuilder.Position = 0;
                                using var reader = new StreamReader(messageBuilder,
                                    Encoding.UTF8, false, DefaultBufferSize, true);
                                fullMessage = await reader.ReadToEndAsync();
                            }

                            Console.WriteLine($"📨 Received {messageBuilder.Length} bytes from {user.Id}: {TruncateMessage(fullMessage, 100)}");

                            // 🔥 Trigger MessageReceived event
                            OnMaanfeeMessageReceived(new MaanfeeMessageReceivedEventArgs
                            {
                                Message = fullMessage,
                                ReceivedTime = DateTime.Now,
                                WebSocket = user.WebSocket,
                                User = user
                            });

                            // 🔥 ارسال پیام به همه کاربران با مدیریت خطا - PRESERVED!
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await BroadcastMessage($"📢 Broadcast from {user.Id}: {TruncateMessage(fullMessage, 50)}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"⚠️ Broadcast error for user {user.Id}: {ex.Message}");
                                }
                            });

                            // 🔥 پاکسازی برای پیام بعدی
                            messageBuilder.SetLength(0);

                            // 🔴 جلوگیری از رشد بی‌رویه
                            messageBuilder.Capacity = Math.Max(DefaultBufferSize, (int)messageBuilder.Capacity);
                        }
                        else
                        {
                            // هنوز پیام کامل نشده - فقط log کنیم
                            Console.WriteLine($"⏳ Receiving chunk for {user.Id}, total so far: {messageBuilder.Length} bytes");
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // 🔥 مدیریت پیام‌های باینری
                        await messageBuilder.WriteAsync(buffer.AsMemory(0, result.Count), CancellationTokenSource.Token);

                        if (result.EndOfMessage)
                        {
                            messageBuilder.Position = 0;
                            var binaryData = messageBuilder.ToArray();

                            Console.WriteLine($"📦 Received binary data ({binaryData.Length} bytes) from {user.Id}");

                            // می‌توانید event جداگانه برای باینری اضافه کنید
                            OnMaanfeeMessageReceived(new MaanfeeMessageReceivedEventArgs
                            {
                                Message = "[BINARY DATA]",
                                ReceivedTime = DateTime.Now,
                                WebSocket = user.WebSocket,
                                User = user
                            });

                            messageBuilder.SetLength(0);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // فقط اتصال را قطع کنید بدون فراخوانی CloseAsync مجدد
                        Console.WriteLine($"🔌 Close message received from {user.Id}");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Server is stopping - this is normal
                Console.WriteLine($"⏹️ Operation canceled for user {user.Id}");
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                // User closed connection unexpectedly
                Console.WriteLine($"⚠️ User {user.Id} closed connection prematurely: {ex.Message}");
            }
            catch (MaanfeeWebSocketException ex)
            {
                Console.WriteLine($"❌ WebSocket error for user {user.Id}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Unexpected error for user {user.Id}: {ex.Message}");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                await CleanupUser(user);
            }
        }

        // 🔥 تابع کمکی برای truncate کردن پیام‌های طولانی
        private static string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
                return message;

            return message.Substring(0, maxLength) + "...";
        }

        protected virtual async Task CleanupUser(MaanfeeWebSocketUser user)
        {
            try
            {
                user.MarkDisconnected();

                lock (_lock)
                {
                    Users.Remove(user);
                }

                if (user.WebSocket.State == WebSocketState.Open || user.WebSocket.State == WebSocketState.CloseReceived || user.WebSocket.State == WebSocketState.CloseSent)
                {
                    try
                    {
                        await user.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "Connection closed",
                            CancellationToken.None);
                    }
                    catch (MaanfeeWebSocketException)
                    {
                        // Ignore close errors during cleanup
                    }
                    catch (ObjectDisposedException)
                    {
                        // Socket already disposed
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during user cleanup {user.Id}: {ex.Message}");
            }
            finally
            {
                try
                {
                    user.WebSocket.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing websocket for {user.Id}: {ex.Message}");
                }

                Console.WriteLine($"User disconnected: {user.Id}. Total users: {Users.Count}");
                OnMaanfeeClientDisconnected(new MaanfeeWebSocketClientEventArgs
                {
                    WebSocket = user.WebSocket,
                    User = user
                });
            }
        }

        protected virtual async Task BroadcastMessage(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var tasks = new List<Task>();

            lock (_lock)
            {
                foreach (var user in Users.Where(u => u.IsConnected && u.WebSocket != null))
                {
                    try
                    {
                        tasks.Add(user.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error queueing send for user {user.Id}: {ex.Message}");
                    }
                }
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    Console.WriteLine($"Broadcast error: {ex.Message}");
                }
            }
        }

        public virtual async Task SendToClientAsync(string clientId, string message)
        {
            var user = GetUserById(clientId);
            if (user != null && user.IsConnected)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await user.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public virtual async Task SendToAllAsync(string message)
        {
            await BroadcastMessage(message);
        }

    }
}
