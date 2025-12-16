namespace Maanfee.WebSocket
{
    public abstract class MaanfeeWebSocketBase : IDisposable
    {
        protected const long MaxMessageSize = 10 * 1024 * 1024;

        protected const int DefaultBufferSize = 4096;

        //private int _bufferSize = 4096;
        //public int BufferSize
        //{
        //    get => _bufferSize;
        //    set => _bufferSize = value < 1024 ?
        //        throw new ArgumentOutOfRangeException(nameof(BufferSize), "Buffer size must be at least 1024 bytes") : value;
        //}

        protected virtual CancellationTokenSource CancellationTokenSource { get; set; }

        // State management
        protected readonly object StateLock = new object();

        #region - Dispose -

        protected bool _isDisposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    CancellationTokenSource?.Dispose();
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
 
    }
}
