using Microsoft.Extensions.Hosting;

namespace Maanfee.WebSocket
{
    /// <summary>Ties the server lifecycle to the host: Start on host start, graceful StopAsync on host stop.</summary>
    public sealed class WebSocketServerHostedService : IHostedService
    {
        private readonly MaanfeeWebSocketServer _server;

        public WebSocketServerHostedService(MaanfeeWebSocketServer server)
        {
            _server = server;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_server.State.CanStart())
                _server.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => _server.StopAsync();
    }
}
