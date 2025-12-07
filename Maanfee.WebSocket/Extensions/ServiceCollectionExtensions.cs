using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maanfee.WebSocket
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMaanfeeWebSocket(this IServiceCollection services)
        {
            services.TryAddSingleton<MaanfeeWebSocketServer>();

            return services;
        }
    }
}
