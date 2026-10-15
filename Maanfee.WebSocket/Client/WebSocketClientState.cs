namespace Maanfee.WebSocket
{
    public enum WebSocketClientState
    {
        /// <summary>
        /// کلاینت هنوز هیچ تلاشی برای اتصال نکرده
        /// </summary>
        Disconnected,

        /// <summary>
        /// در حال اتصال به سرور
        /// </summary>
        Connecting,

        /// <summary>
        /// با موفقیت متصل شده و آماده ارسال/دریافت
        /// </summary>
        Connected,

        /// <summary>
        /// در حال قطع ارتباط
        /// </summary>
        Disconnecting,

        /// <summary>
        /// مجددا در حال اتصال (برای AutoRetry)
        /// </summary>
        Reconnecting,

        /// <summary>
        /// خطا رخ داده و ارتباط قطع شده
        /// </summary>
        Faulted,

        /// <summary>
        /// کلاینت disposable شده
        /// </summary>
        Disposed
    }

    public static class WebSocketClientStateExtensions
    {
        public static bool CanSend(this WebSocketClientState state)
             => state == WebSocketClientState.Connected;

        public static bool CanConnect(this WebSocketClientState state)
            => state == WebSocketClientState.Disconnected ||
               state == WebSocketClientState.Faulted;

        public static bool IsTerminal(this WebSocketClientState state)
            => state == WebSocketClientState.Disconnected ||
               state == WebSocketClientState.Faulted ||
               state == WebSocketClientState.Disposed;
    }
}
