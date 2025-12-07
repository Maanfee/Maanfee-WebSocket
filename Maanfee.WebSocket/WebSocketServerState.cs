namespace Maanfee.WebSocket
{
    public enum WebSocketServerState
    {
        /// <summary>
        /// سرور متوقف شده
        /// </summary>
        Stopped,

        /// <summary>
        /// در حال شروع به کار
        /// </summary>
        Starting,

        /// <summary>
        /// در حال اجرا و پذیرش اتصال‌ها
        /// </summary>
        Running,

        /// <summary>
        /// در حال توقف
        /// </summary>
        Stopping,

        /// <summary>
        /// خطا رخ داده
        /// </summary>
        Faulted
    }

    public static class WebSocketServerStateExtensions
    {
        public static bool CanAcceptConnections(this WebSocketServerState state)
            => state == WebSocketServerState.Running;

        public static bool CanStart(this WebSocketServerState state)
            => state == WebSocketServerState.Stopped ||
               state == WebSocketServerState.Faulted;

        public static bool CanStop(this WebSocketServerState state)
            => state == WebSocketServerState.Running;
    }
}