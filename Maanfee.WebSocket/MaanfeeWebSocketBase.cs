using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maanfee.WebSocket
{
    public abstract class MaanfeeWebSocketBase : IDisposable, IAsyncDisposable
    {
        protected ILogger Logger { get; }

        protected MaanfeeWebSocketBase(ILogger logger = null)
        {
            Logger = logger ?? NullLogger.Instance;
        }

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

        /// <summary>Asynchronous cleanup (graceful stop/disconnect). Runs before the synchronous release in <see cref="Dispose(bool)"/>.</summary>
        protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Releases synchronous resources only; it never waits. Use <see cref="DisposeAsync"/> for a graceful shutdown.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
 
    }
}
