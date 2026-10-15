using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maanfee.WebSocket
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMaanfeeWebSocket(this IServiceCollection services, Action<MaanfeeWebSocketServerOptions> configureOptions = null)
        {
            var options = new MaanfeeWebSocketServerOptions();
            configureOptions?.Invoke(options);
            services.TryAddSingleton(options);
            services.TryAddSingleton<IWebSocketConnectionManager, WebSocketConnectionManager>();
            services.TryAddSingleton<IWebSocketMessageSender, WebSocketMessageSender>();
            services.TryAddSingleton<MaanfeeWebSocketServer>();
            services.AddHostedService<WebSocketServerHostedService>();

            return services;
        }

        //public static IServiceCollection AddMaanfeeWebSocket(this IServiceCollection services,
        //    Action<MaanfeeWebSocketServerOptions> configureOptions)
        //{
        //    var options = new MaanfeeWebSocketServerOptions();
        //    configureOptions?.Invoke(options);

        //    services.TryAddSingleton<MaanfeeWebSocketServer>(sp =>
        //        new MaanfeeWebSocketServer(options));

        //    return services;
        //}

        //        builder.Services.AddMaanfeeWebSocket(options =>
        //{
        //    options.Host = "127.0.0.1";
        //    options.Port = 5000;
        //    options.BufferSize = 4096;
        //    options.AutoRetryConnection = true;
        //    options.RetryCount = 3;
        //    options.RetryDelay = TimeSpan.FromSeconds(10);
        //});

    }
}
