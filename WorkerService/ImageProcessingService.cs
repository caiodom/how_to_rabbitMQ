using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio.DataModel.Args;
using Minio;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Core.Interfaces;
using Core.Contracts;
using CoreAdapters.Interfaces.Configuration;
using CoreAdapters.Extensions;
using Microsoft.Extensions.Options;
using Core.Utils;
using CoreAdapters.Services;

namespace WorkerService
{
    public class ImageProcessingService : BackgroundService
    {

        private readonly ILogger<ImageProcessingService> _logger;
        private readonly IMinioService _minioServices;
        private IModel _model;
        private readonly IRabbitMQConnectionService _rabbitMQConnectionService;
        private readonly IFilterService _filterService;

        private readonly string EXCHANGE_NAME;
        private readonly string QUEUE_NAME;
        private readonly string ROUTING_KEY;
        private readonly string MINIO_NOT_PROCESSED_IMAGES;
        private readonly string MINIO_PROCESSED_IMAGES;





        public ImageProcessingService(IMinioService minioServices,
                                      IOptions<MinioBucketSettings> minioBucketsConfig,
                                      IOptions<RabbitMQProcessSettings> rabbitMQConfigSettings,
                                      IRabbitMQConnectionService rabbitMQConnectionService,
                                      ILogger<ImageProcessingService> logger,
                                      IFilterService filterService)
        {
            _logger = logger;
            _filterService = filterService;
            _minioServices = minioServices;
            _rabbitMQConnectionService = rabbitMQConnectionService;

            EXCHANGE_NAME = rabbitMQConfigSettings.Value.ExchangeName;
            QUEUE_NAME = rabbitMQConfigSettings.Value.QueueName;
            ROUTING_KEY = rabbitMQConfigSettings.Value.RoutingKey;
            MINIO_NOT_PROCESSED_IMAGES = minioBucketsConfig.Value.MinioBucketNotProcessedImages;
            MINIO_PROCESSED_IMAGES = minioBucketsConfig.Value.MinioBucketProcessedImages;


        }



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            _model = this.BuildModel();

            var consumer = this.BuildConsumer();

            string consumerTag = consumer
                                     .Model
                                     .BasicConsume(queue: QUEUE_NAME,
                                                   autoAck: false,
                                                   consumer: consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogTrace("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }

            _model.BasicCancelNoWait(consumerTag);

        }
        private IModel BuildModel()
        {

            var model = _rabbitMQConnectionService.GetConnection().CreateModel();

            model.ExchangeDeclare(exchange: EXCHANGE_NAME,
                                   type: ExchangeType.Direct);

            model.QueueDeclare(queue: QUEUE_NAME,
                                durable: true,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);


            model.QueueBind(queue: QUEUE_NAME,
                             exchange: EXCHANGE_NAME,
                             routingKey: ROUTING_KEY);

            model.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

            return model;
        }


        private async Task<RabbitMQConsumeHandler> ProcessImageAsync(ImageProcessingRequest request)
        {
            var imageName = Path.GetFileName(request.ImageUrl);
            string imagePath = Path.Combine(Directory.GetCurrentDirectory(), imageName);


            MemoryStream streamToReturn = new MemoryStream();
            await _minioServices.GetObjectAsync(streamToReturn, MINIO_NOT_PROCESSED_IMAGES, imageName);


            File.WriteAllBytes(imagePath, streamToReturn.ToArray());

            _logger.LogInformation($"Image downloaded from MinIO: {request.ImageUrl}");


            await _filterService.FilterHandler(imagePath, request.FilterType, request.ContentType);

            var processedImageName = $"processed_{request.FilterType}_{imageName}";

            using var outputStream = new FileStream(imagePath, FileMode.Open);

            var response = await _minioServices.PutObjectAsync(MINIO_PROCESSED_IMAGES, processedImageName, outputStream, request.ContentType);


            if (processedImageName != response.ToString())
            {
                _logger.LogInformation($"Error when image saved to MinIO: {processedImageName}");
                return RabbitMQConsumeHandler.NACK;
            }


            _logger.LogInformation($"Processed image uploaded to MinIO: {processedImageName}");

            File.Delete(imagePath);


            return RabbitMQConsumeHandler.ACK;

        }

        public IBasicConsumer BuildConsumer()
        {
            var consumer = new AsyncEventingBasicConsumer(_model);

            consumer.Received += Receive;

            return consumer;
        }


        private async Task Receive(object sender, BasicDeliverEventArgs receivedItem)
        {
            RabbitMQConsumeHandler action = DeserializeHandler(receivedItem, out ImageProcessingRequest request);

            if (action is RabbitMQConsumeHandler.READY)
            {
                try
                {
                   action= await ProcessImageAsync(request);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing request. Error: {ex.Message}");
                    action=RabbitMQConsumeHandler.NACK;
                }

            }

            ConsumerHandler(receivedItem,action);

        }

        private void ConsumerHandler(BasicDeliverEventArgs receivedItem,RabbitMQConsumeHandler rabbitMQConsumeHandler)
        {
            switch(rabbitMQConsumeHandler)
            {
                case RabbitMQConsumeHandler.READY: throw new InvalidOperationException("Invalid consumer action");
                case RabbitMQConsumeHandler.ACK: _model.BasicAck(deliveryTag: receivedItem.DeliveryTag, multiple: false); break;
                case RabbitMQConsumeHandler.NACK: _model.BasicNack(deliveryTag: receivedItem.DeliveryTag, multiple: false, requeue: false); break;
                case RabbitMQConsumeHandler.REJECT:_model.BasicReject(deliveryTag: receivedItem.DeliveryTag, requeue: false); break;
            }
        }

        private RabbitMQConsumeHandler DeserializeHandler(BasicDeliverEventArgs receivedItem, out ImageProcessingRequest request)
        {
            request = default;
            try
            {
                var message = ValidateBasicDeliverEventArgs(receivedItem);
                _logger.LogInformation($"Received message: {message}");
                request = JsonConvert.DeserializeObject<ImageProcessingRequest>(message);

                if (request is null)
                    throw new InvalidOperationException("Failed to deserialize the message. The message format may be incorrect or missing required fields.");

                return RabbitMQConsumeHandler.READY;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Message rejected during desserialization {ex}", ex);
                return RabbitMQConsumeHandler.REJECT;
            }

        }

        private string ValidateBasicDeliverEventArgs(BasicDeliverEventArgs receivedItem)
        {
            if (receivedItem == null)
                throw new ArgumentNullException(nameof(receivedItem));

            var body = receivedItem.Body.ToArray();

            if (body == null || body.Length == 0)
                throw new InvalidOperationException("The message body is empty or null. Cannot process a message without content.");

            var message = Encoding.UTF8.GetString(body);

            if (message == null)
                throw new InvalidOperationException("Failed to convert the message body to a string. Ensure the message content is properly encoded.");

            return message;
        }

    }



}
