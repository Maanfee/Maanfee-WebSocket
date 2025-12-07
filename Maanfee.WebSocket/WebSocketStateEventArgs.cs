namespace Maanfee.WebSocket
{
    public class WebSocketStateChangedEventArgs : EventArgs
    {
        public object OldState { get; set; }
        public object NewState { get; set; }
        public DateTime ChangeTime { get; set; } = DateTime.Now;
        public string Reason { get; set; }
    }

    // یا generic version برای type safety بهتر:
    public class WebSocketStateChangedEventArgs<T> : EventArgs where T : Enum
    {
        public T OldState { get; set; }
        public T NewState { get; set; }
        public DateTime ChangeTime { get; set; } = DateTime.Now;
        public string Reason { get; set; }

        public override string ToString()
            => $"[{ChangeTime:HH:mm:ss.fff}] {OldState} → {NewState} (Reason: {Reason})";
    }
}
