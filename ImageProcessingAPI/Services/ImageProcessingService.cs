using Core.Contracts;
using Core.Interfaces;
using CoreAdapters.Interfaces.Configuration;
using ImageProcessingAPI.Services.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;


namespace ImageProcessingAPI.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly IFileValidator _fileValidator;
        private readonly IMinioServices _minioServices;
        private readonly string EXCHANGE_NAME;
        private readonly string QUEUE_NAME;
        private readonly string ROUTING_KEY;
        private readonly string MINIO_NOT_PROCESSED_IMAGES;
        private readonly string MINIO_PROCESSED_IMAGES;

        public ImageProcessingService(IMinioServices minioServices,
                                         IRabbitMQConnectionService rabbitMQConnectionService,
                                         IOptions<MinioBucketSettings> minioBucketsConfig,
                                         IOptions<RabbitMQProcessSettings> rabbitMQConfigSettings,
                                         IFileValidator fileValidator)
        {

            _connection = rabbitMQConnectionService.GetConnection();
            _channel = _connection.CreateModel();
            _minioServices = minioServices;
            _fileValidator = fileValidator;

            EXCHANGE_NAME = rabbitMQConfigSettings.Value.ExchangeName;
            QUEUE_NAME = rabbitMQConfigSettings.Value.QueueName;
            ROUTING_KEY = rabbitMQConfigSettings.Value.RoutingKey;
            MINIO_NOT_PROCESSED_IMAGES = minioBucketsConfig.Value.MinioBucketNotProcessedImages;
            MINIO_PROCESSED_IMAGES = minioBucketsConfig.Value.MinioBucketProcessedImages;
        }


        public async Task<string> ProcessImage(IFormFile image, string filterType)
        {
            if (!_fileValidator.IsValid(image))
                throw new InvalidOperationException("Unsupported file format.");

            var codigoImagem = Guid.NewGuid();
            string imageName = $"{codigoImagem}_{image.FileName}";


            using (var stream = image.OpenReadStream())
            {
                var response = await _minioServices
                                        .PutObjectAsync(MINIO_NOT_PROCESSED_IMAGES,
                                                        imageName,
                                                        stream,
                                                        image.ContentType);
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
            using (var streamToReturn = new MemoryStream())
            {
                await _minioServices.GetObjectAsync(streamToReturn,MINIO_PROCESSED_IMAGES, fileName);
                return streamToReturn.ToArray();
            }
           
        }
    }
}
