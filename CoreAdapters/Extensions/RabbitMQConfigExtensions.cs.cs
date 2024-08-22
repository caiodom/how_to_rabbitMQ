
using CoreAdapters.Interfaces.Configuration;
using Core.Contracts;
using CoreAdapters.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreAdapters.Extensions
{
    public static class RabbitMQConfigExtensions
    {
        public static void AddRabbitMQConfigurations(this IServiceCollection services)
        {

            services.AddSingleton<IRabbitMQConnectionService>(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<RabbitMQSettings>>().Value ?? throw new InvalidOperationException("The RabbitMQSettings is not set or is null.");
                return new RabbitMQConnectionService(settings);
            });

        }
    }
}
