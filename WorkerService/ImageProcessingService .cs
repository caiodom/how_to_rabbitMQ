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
            ROUTING_KEY=rabbitMQConfigSettings.Value.RoutingKey;
            MINIO_NOT_PROCESSED_IMAGES= minioBucketsConfig.Value.MinioBucketNotProcessedImages;
            MINIO_PROCESSED_IMAGES=minioBucketsConfig.Value.MinioBucketProcessedImages;


        }



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await MinioConfigExtensions.MinioBucketHandler(_minioClient, MINIO_PROCESSED_IMAGES);

            _model = this.BuildModel();

            var consumer = this.BuildConsumer();

            //this.WaitQueueCreation();

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


        private async Task ProcessImageAsync(ImageProcessingRequest request)
        {
            var imageName = Path.GetFileName(request.ImageUrl);
            string imagePath = Path.Combine(Directory.GetCurrentDirectory(), imageName);


            MemoryStream streamToReturn = new MemoryStream();

            await _minioClient.GetObjectAsync(new GetObjectArgs()
                //.WithBucket(_minioBucketName)
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

        }




        public IBasicConsumer BuildConsumer()
        {
            var consumer = new AsyncEventingBasicConsumer(_model);

            consumer.Received += Receive;

            return consumer;
        }


        private async Task Receive(object sender, BasicDeliverEventArgs receivedItem)
        {
            if (receivedItem == null)
                throw new ArgumentNullException(nameof(receivedItem));

            var open = _model.IsOpen;


            var body = receivedItem.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            _logger.LogInformation($"Received message: {message}");

            try
            {
                var request = JsonConvert.DeserializeObject<ImageProcessingRequest>(message) ?? throw new InvalidOperationException("request não pode ser nula!");
                await ProcessImageAsync(request);
                open = _model.IsOpen;
                _model.BasicAck(deliveryTag: receivedItem.DeliveryTag, multiple:false);

            }
            catch (Exception ex)
            {

                _model.BasicNack(deliveryTag: receivedItem.DeliveryTag, multiple: false, requeue: false);
                _logger.LogError($"Error processing message: {message}. Error: {ex.Message}");
            }

        }


        private async Task MinioBucketHandler()
        {
            //reformular em uma classe externa para aplicar o SOLID:
            var bktExistsArgs = new BucketExistsArgs().WithBucket(MINIO_PROCESSED_IMAGES);
            bool found = await _minioClient.BucketExistsAsync(bktExistsArgs);


            if (!found)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(MINIO_PROCESSED_IMAGES);
                await _minioClient.MakeBucketAsync(makeBucketArgs);
                Console.WriteLine(MINIO_PROCESSED_IMAGES + " created successfully");
            }
            else
            {
                Console.WriteLine(MINIO_PROCESSED_IMAGES + " already existis.");
            }
        }

        private void WaitQueueCreation()
        {
            Policy
                .Handle<OperationInterruptedException>()
                .WaitAndRetry(5, retryAttempt =>
                {
                    TimeSpan timeToWait = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    _logger.LogWarning("Queue {queueName} not found... We will try in {tempo}.", QUEUE_NAME, timeToWait.TotalSeconds);
                    return timeToWait;

                }).Execute(() =>
                {
                    var open = _model.IsOpen;
                    using IModel testModel = this.BuildModel();
                    testModel.QueueDeclarePassive(QUEUE_NAME);
                    open = _model.IsOpen;


                });



        }

        public override void Dispose()
        {
           /* _model.Close();
            _connection.Close();
            base.Dispose();*/
        }
    }



}
