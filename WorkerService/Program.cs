using Core;
using Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using WorkerService;

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
    services.AddHostedService<ImageProcessingService>();

    services.AddSingleton<IFilterService, FilterService>();

    string endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT");
    string accessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY");
    string secretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY");
    bool useSSL = bool.TryParse(Environment.GetEnvironmentVariable("MINIO_USE_SSL"), out useSSL);

   /* // Add Minio using the default endpoint
    services.AddMinio(accessKey, secretKey);*/

    // Add Minio using the custom endpoint and configure additional settings for default MinioClient initialization
    services.AddMinio(configureClient => configureClient
        .WithEndpoint(endpoint)
        .WithCredentials(accessKey, secretKey)
        .WithTimeout(TimeSpan.FromMinutes(5).Minutes)
        .WithSSL(false)
        .Build());
}