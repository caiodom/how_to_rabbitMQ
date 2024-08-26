using Microsoft.Extensions.DependencyInjection;
using Minio;
using Minio.DataModel.Args;


namespace CoreAdapters.Extensions
{
    public static class MinioConfigExtensions
    {

        public static IServiceCollection AddMinioConfigurations(this IServiceCollection services)
        {
            string endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT") ?? throw new InvalidOperationException("The MINIO_SECRET_KEY environment variable is not set or is null.");
            string accessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY") ?? throw new InvalidOperationException("The MINIO_ACCESS_KEY environment variable is not set or is null.");
            string secretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY") ?? throw new InvalidOperationException("The MINIO_SECRET_KEY environment variable is not set or is null.");
            bool useSSL = bool.TryParse(Environment.GetEnvironmentVariable("MINIO_USE_SSL") ?? throw new InvalidOperationException("The MINIO_USE_SSL environment variable is not set or is null."), out useSSL);

            services.AddMinio(configureClient => configureClient
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithTimeout(TimeSpan.FromMinutes(5).Minutes)
                .WithSSL(false)
                .Build());

            return services;
        }


        public static async Task MinioBucketHandler(IMinioClient minioClient,string bucketName)
        {
            var bktExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
            bool found = await minioClient.BucketExistsAsync(bktExistsArgs);


            if (!found)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(bucketName);
                await minioClient.MakeBucketAsync(makeBucketArgs);
                Console.WriteLine(bucketName + " created successfully");
            }
            else
            {
                Console.WriteLine(bucketName + " already existis.");
            }
        }
    }
}
