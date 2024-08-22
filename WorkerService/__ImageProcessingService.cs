using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio.DataModel.Args;
using Minio;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Core.Interfaces;
using Core.Contracts;
using CoreAdapters.Interfaces.Configuration;
using CoreAdapters.Extensions;

namespace WorkerService
{
    public class DeprecatedImageProcessingService : BackgroundService
    {
        private readonly ILogger<ImageProcessingService> _logger;
        private readonly IMinioClient _minioClient;
        private readonly IFilterService _filterService;
        private readonly IRabbitMQConnectionService _rabbitMQConnectionService;

        private IConnection _connection;
        private IModel _channel;

        private const string EXCHANGE_NAME = "image_processing_exchange";
        private const string QUEUE_NAME = "image_processing_queue";
        private const string ROUTING_KEY = "image.process";
        private const string MINIO_NOT_PROCESSED_IMAGES = "minhas-imagens";
        private const string MINIO_PROCESSED_IMAGES = "processed-images";

        public DeprecatedImageProcessingService(
            IMinioClient minioClient,
            IRabbitMQConnectionService rabbitMQConnectionService,
            ILogger<ImageProcessingService> logger,
            IFilterService filterService)
        {
            _logger = logger;
            _filterService = filterService;
            _minioClient = minioClient;
            _rabbitMQConnectionService = rabbitMQConnectionService;
            InitializeRabbitMQ();
        }

        private void InitializeRabbitMQ()
        {
            _connection = _rabbitMQConnectionService.GetConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(exchange: EXCHANGE_NAME, type: ExchangeType.Direct);
            _channel.QueueDeclare(queue: QUEUE_NAME, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue: QUEUE_NAME, exchange: EXCHANGE_NAME, routingKey: ROUTING_KEY);

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await EnsureMinioBucketExistsAsync();

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                var message = Encoding.UTF8.GetString(ea.Body.ToArray());
                _logger.LogInformation($"Received message: {message}");

                try
                {
                    var request = JsonConvert.DeserializeObject<ImageProcessingRequest>(message);
                    await ProcessImageAsync(request, ea.DeliveryTag);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing message: {message}. Error: {ex.Message}");
                    await HandleProcessingErrorAsync(ea.DeliveryTag);
                }
            };



            var consumerTag=_channel.BasicConsume(queue: QUEUE_NAME, autoAck: false, consumer: consumer);

            //mantém o serviço em execução
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogTrace("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }

            this._channel.BasicCancelNoWait(consumerTag);
        }

        private async Task ProcessImageAsync(ImageProcessingRequest request, ulong deliveryTag)
        {
            using var channel = _connection.CreateModel();

            try
            {
                var imageName = Path.GetFileName(request.ImageUrl);
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), imageName);

                await DownloadImageFromMinioAsync(imageName, imagePath);
                await _filterService.FilterHandler(imagePath, request.FilterType, request.ContentType);
                await UploadProcessedImageToMinioAsync(request, imageName, imagePath);

                File.Delete(imagePath);

                channel.BasicAck(deliveryTag, multiple: false);
                _logger.LogInformation($"Successfully processed and acknowledged message: {request.ImageUrl}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during image processing: {ex.Message}");
                channel.BasicNack(deliveryTag, multiple: false, requeue: false);
            }
        }

        private async Task DownloadImageFromMinioAsync(string imageName, string imagePath)
        {
            using var streamToReturn = new MemoryStream();
            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(MINIO_NOT_PROCESSED_IMAGES)
                .WithObject(imageName)
                .WithCallbackStream(stream => stream.CopyTo(streamToReturn)));

            await File.WriteAllBytesAsync(imagePath, streamToReturn.ToArray());
            _logger.LogInformation($"Image downloaded from MinIO: {imageName}");
        }

        private async Task UploadProcessedImageToMinioAsync(ImageProcessingRequest request, string imageName, string imagePath)
        {
            var processedImageName = $"processed_{request.FilterType}_{imageName}";

            using var outputStream = new FileStream(imagePath, FileMode.Open);
            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(MINIO_PROCESSED_IMAGES)
                .WithObject(processedImageName)
                .WithStreamData(outputStream)
                .WithObjectSize(outputStream.Length)
                .WithContentType(request.ContentType));

            _logger.LogInformation($"Processed image uploaded to MinIO: {processedImageName}");
        }

        private async Task EnsureMinioBucketExistsAsync()
        {
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(MINIO_PROCESSED_IMAGES);
            bool found = await _minioClient.BucketExistsAsync(bucketExistsArgs);

            if (!found)
            {
                await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(MINIO_PROCESSED_IMAGES));
                _logger.LogInformation($"{MINIO_PROCESSED_IMAGES} created successfully");
            }
            else
            {
                _logger.LogInformation($"{MINIO_PROCESSED_IMAGES} already exists.");
            }
        }

        private Task HandleProcessingErrorAsync(ulong deliveryTag)
        {
            using var channel = _connection.CreateModel();
            channel.BasicNack(deliveryTag, multiple: false, requeue: false);
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
