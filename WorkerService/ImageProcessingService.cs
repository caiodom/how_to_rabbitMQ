using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio.DataModel.Args;
using Minio;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Core.Interfaces;
using Core.Contracts;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;
using CoreAdapters.Interfaces.Configuration;
using CoreAdapters.Extensions;
using Polly;
using RabbitMQ.Client.Exceptions;
using Microsoft.Extensions.Options;
using Core.Utils;

namespace WorkerService
{
    public class ImageProcessingService : BackgroundService
    {

        private readonly ILogger<ImageProcessingService> _logger;
        private readonly IMinioClient _minioClient;
        private IConnection _connection;
        private IModel _model;
        private readonly IRabbitMQConnectionService _rabbitMQConnectionService;
        private readonly IFilterService _filterService;

        private readonly string EXCHANGE_NAME;
        private readonly string QUEUE_NAME;
        private readonly string ROUTING_KEY;
        private readonly string MINIO_NOT_PROCESSED_IMAGES;
        private readonly string MINIO_PROCESSED_IMAGES;





        public ImageProcessingService(IMinioClient minioClient,
                                      IOptions<MinioBucketSettings> minioBucketsConfig,
                                      IOptions<RabbitMQProcessSettings> rabbitMQConfigSettings,
                                      IRabbitMQConnectionService rabbitMQConnectionService,
                                      ILogger<ImageProcessingService> logger,
                                      IFilterService filterService)
        {
            _logger = logger;
            _filterService = filterService;
            _minioClient = minioClient;
            _rabbitMQConnectionService = rabbitMQConnectionService;

            EXCHANGE_NAME = rabbitMQConfigSettings.Value.ExchangeName;
            QUEUE_NAME = rabbitMQConfigSettings.Value.QueueName;
            ROUTING_KEY = rabbitMQConfigSettings.Value.RoutingKey;
            MINIO_NOT_PROCESSED_IMAGES = minioBucketsConfig.Value.MinioBucketNotProcessedImages;
            MINIO_PROCESSED_IMAGES = minioBucketsConfig.Value.MinioBucketProcessedImages;


        }



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await MinioConfigExtensions.MinioBucketHandler(_minioClient, MINIO_PROCESSED_IMAGES);

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
            _connection = _rabbitMQConnectionService.GetConnection();
            _model = _connection.CreateModel();

            _model.ExchangeDeclare(exchange: EXCHANGE_NAME,
                                   type: ExchangeType.Direct);

            _model.QueueDeclare(queue: QUEUE_NAME,
                                durable: true,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);


            _model.QueueBind(queue: QUEUE_NAME,
                             exchange: EXCHANGE_NAME,
                             routingKey: ROUTING_KEY);

            _model.BasicQos(prefetchSize: 0, prefetchCount: 12000, global: false);

            return _model;
        }


        private async Task<RabbitMQConsumeHandler> ProcessImageAsync(ImageProcessingRequest request)
        {
            var imageName = Path.GetFileName(request.ImageUrl);
            string imagePath = Path.Combine(Directory.GetCurrentDirectory(), imageName);


            MemoryStream streamToReturn = new MemoryStream();

            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(MINIO_NOT_PROCESSED_IMAGES)
                .WithObject(imageName)
                .WithCallbackStream((stream) =>
                {
                    stream.CopyTo(streamToReturn);
                }));



            File.WriteAllBytes(imagePath, streamToReturn.ToArray());

            _logger.LogInformation($"Image downloaded from MinIO: {request.ImageUrl}");


            await _filterService.FilterHandler(imagePath, request.FilterType, request.ContentType);

            var processedImageName = $"processed_{request.FilterType}_{imageName}";

            using var outputStream = new FileStream(imagePath, FileMode.Open);

            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(MINIO_PROCESSED_IMAGES)
                .WithObject(processedImageName)
                .WithStreamData(outputStream)
                .WithObjectSize(outputStream.Length)
                .WithContentType(request.ContentType));

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
