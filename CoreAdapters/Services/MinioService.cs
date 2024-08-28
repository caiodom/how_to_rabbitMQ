using Minio.DataModel.Args;
using Minio.DataModel;
using Minio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Minio.ApiEndpoints;
using Core.Interfaces;

namespace CoreAdapters.Services
{
    public class MinioService: IMinioService
    {
        private IMinioClient _minioClient;

        // Construtor que inicializa o cliente do MinIO
        public MinioService()
        {
            Connect();
        }

        public async Task GetObjectAsync(MemoryStream memoryStream, string bucketName, string objectName)
        {
            await MinioBucketHandler(bucketName);


            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream((stream) =>
                {
                    stream.CopyTo(memoryStream);
                }));
        }

        //TODO:Resolve this
        public async Task<string> PutObjectAsync(string bucketName,
                                            string processedImageName,
                                            FileStream outputStream,
                                            string contentType)
        {
            await MinioBucketHandler(bucketName);

            var response = await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(processedImageName)
                .WithStreamData(outputStream)
                .WithObjectSize(outputStream.Length)
                .WithContentType(contentType));

            return response.ObjectName;
        }


        //TODO:Resolve this
        public async Task<string> PutObjectAsync(string bucketName,
                                            string processedImageName,
                                            Stream outputStream,
                                            string contentType)
        {
            await MinioBucketHandler(bucketName);

            var response = await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(processedImageName)
                .WithStreamData(outputStream)
                .WithObjectSize(outputStream.Length)
                .WithContentType(contentType));

            return response.ObjectName;
        }


        public async Task MinioBucketHandler(string bucketName)
        {
            var bktExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
            bool found = await _minioClient.BucketExistsAsync(bktExistsArgs);


            if (!found)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(bucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs);
                Console.WriteLine(bucketName + " created successfully");
            }
            else
            {
                Console.WriteLine(bucketName + " already existis.");
            }
        }


        private void Connect()
        {
            string endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT") ?? throw new InvalidOperationException("The MINIO_SECRET_KEY environment variable is not set or is null.");
            string accessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY") ?? throw new InvalidOperationException("The MINIO_ACCESS_KEY environment variable is not set or is null.");
            string secretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY") ?? throw new InvalidOperationException("The MINIO_SECRET_KEY environment variable is not set or is null.");
            bool useSSL = bool.TryParse(Environment.GetEnvironmentVariable("MINIO_USE_SSL") ?? throw new InvalidOperationException("The MINIO_USE_SSL environment variable is not set or is null."), out useSSL);



            _minioClient = new MinioClient()
                                .WithEndpoint(endpoint)
                                    .WithCredentials(accessKey, secretKey)
                                    .WithTimeout(TimeSpan.FromMinutes(5).Minutes)
                                    .WithSSL(false)
                                    .Build();

        }
    }
}
