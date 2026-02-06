#nullable enable
using System;
using Microsoft.Extensions.DependencyInjection;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Extension methods for configuring MulticastTransportFramework with Dependency Injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the MulticastTransportFramework services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">A delegate to configure the socket options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMulticastTransport(this IServiceCollection services, Action<MulticastSocketOptions> configure)
        {
            var options = new MulticastSocketOptions();
            configure(options);

            services.AddSingleton(options);
            services.AddSingleton<TransportComponent>();

            // If using Microsoft.Extensions.Hosting, user can add: services.AddHostedService(sp => sp.GetRequiredService<TransportComponent>());
            // But since TransportComponent is not IHostedService directly (yet), we just register it as Singleton.

            return services;
        }
    }
}
