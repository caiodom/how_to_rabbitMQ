using Core;
using CoreAdapters.Extensions;
using Core.Contracts;
using Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkerService;
using CoreAdapters.Services;


Host.CreateDefaultBuilder(args)
                .ConfigureServices(Configure)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();

                }).Build()
                  .Run();


static void Configure(HostBuilderContext hostContext,IServiceCollection services)
{

    services.Configure<RabbitMQSettings>(hostContext.Configuration.GetSection("RabbitMQ:RabbitMQConnection"));
    services.Configure<RabbitMQProcessSettings>(hostContext.Configuration.GetSection("RabbitMQ:RabbitMQProcess"));
    services.Configure<MinioBucketSettings>(hostContext.Configuration.GetSection("Minio"));
    services.AddMinioConfigurations();

    services.AddSingleton<IFilterService, FilterService>();
    services.AddSingleton<IMinioServices, MinioServices>();

    
    services.AddRabbitMQConfigurations();


    services.AddHostedService<ImageProcessingService>();

}