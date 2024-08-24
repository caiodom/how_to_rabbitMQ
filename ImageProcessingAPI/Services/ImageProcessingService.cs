using Core.Contracts;
using Core.Interfaces;
using CoreAdapters.Extensions;
using CoreAdapters.Interfaces.Configuration;
using ImageProcessingAPI.Controllers;
using ImageProcessingAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;
using System.Threading.Channels;

namespace ImageProcessingAPI.Services
{
    public class ImageProcessingService: IImageProcessingService
    {
        private readonly IMinioClient _minioClient;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string EXCHANGE_NAME;
        private readonly string QUEUE_NAME;
        private readonly string ROUTING_KEY;
        private readonly string MINIO_NOT_PROCESSED_IMAGES;
        private readonly string MINIO_PROCESSED_IMAGES;

        public ImageProcessingService(IMinioClient minioClient,
                                         IRabbitMQConnectionService rabbitMQConnectionService,
                                         IOptions<MinioBucketSettings> minioBucketsConfig,
                                         IOptions<RabbitMQProcessSettings> rabbitMQConfigSettings)
        {

            _connection = rabbitMQConnectionService.GetConnection();
            _channel = _connection.CreateModel();
            _minioClient = minioClient;

            EXCHANGE_NAME = rabbitMQConfigSettings.Value.ExchangeName;
            QUEUE_NAME = rabbitMQConfigSettings.Value.QueueName;
            ROUTING_KEY = rabbitMQConfigSettings.Value.RoutingKey;
            MINIO_NOT_PROCESSED_IMAGES = minioBucketsConfig.Value.MinioBucketNotProcessedImages;
            MINIO_PROCESSED_IMAGES = minioBucketsConfig.Value.MinioBucketProcessedImages;
        }


        public async Task<string> ProcessImage(IFormFile image, string filterType)
        {
            await MinioConfigExtensions.MinioBucketHandler(_minioClient, MINIO_NOT_PROCESSED_IMAGES);

            var codigoImagem = Guid.NewGuid();
            string imageName = $"{codigoImagem}_{image.FileName}";


                using (var stream = image.OpenReadStream())
                {
                    await _minioClient.PutObjectAsync(new PutObjectArgs()
                        .WithBucket(MINIO_NOT_PROCESSED_IMAGES)
                        .WithObject(imageName)
                        .WithStreamData(stream)
                        .WithObjectSize(image.Length)
                        .WithContentType(image.ContentType));
                }

                var message = new ImageProcessingRequest
                {
                    ImageUrl = imageName,
                    FilterType = filterType,
                    ContentType = image.ContentType
                };

                var messageBody = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;// Garante que a mensagem seja persistente


                _channel.BasicPublish(exchange: EXCHANGE_NAME,
                                      routingKey: ROUTING_KEY,
                                      basicProperties: properties,
                                      body: messageBody);


                return $"processed_{filterType}_{imageName}";

        }
        public async Task<byte[]> Download(string fileName)
        {

               MemoryStream streamToReturn = new MemoryStream();

                await _minioClient.GetObjectAsync(new GetObjectArgs()
                                          .WithBucket(MINIO_PROCESSED_IMAGES)
                                          .WithObject(fileName)
                                          .WithCallbackStream((stream) =>
                                          {
                                              stream.CopyTo(streamToReturn);
                                          }));


                return streamToReturn.ToArray();


        }
    }
}
