using Core.Contracts;
using Core.Interfaces;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreAdapters.Services
{
    public class MinioServices: IMinioServices
    {
        private readonly IMinioClient _minioClient;


        public MinioServices(IMinioClient minioClient)
        {
            _minioClient=minioClient;
        }

        public async Task GetObjectAsync(MemoryStream memoryStream,string bucketName, string objectName)
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

            var response= await _minioClient.PutObjectAsync(new PutObjectArgs()
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

    }
}
