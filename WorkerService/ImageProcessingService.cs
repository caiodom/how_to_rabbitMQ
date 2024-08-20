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

namespace WorkerService
{
    public class ImageProcessingService : BackgroundService
    {
        private readonly string _rabbitMqHost = "localhost";
        private readonly ILogger<ImageProcessingService> _logger;
        private readonly IMinioClient _minioClient;
        private IConnection _connection;
        private IModel _model;
        private bool _isConnected = false;
        private const string EXCHANGE_NAME = "image_processing_exchange";
        private const string QUEUE_NAME = "image_processing_queue";
        private const string ROUTING_KEY = "image.process";

        private readonly IFilterService _filterService;

        private readonly string _minioEndpoint = "localhost:9000";
        private readonly string _minioAccessKey = "minioadmin";
        private readonly string _minioSecretKey = "minioadmin";
        private readonly string MINIO_NOT_FORMATTED_IMAGES = "minhas-imagens";
        private readonly string _minioBucketName = "processed-images";



        public ImageProcessingService(IMinioClient minioClient, ILogger<ImageProcessingService> logger, IFilterService filterService)
        {
            _logger = logger;
            _filterService = filterService;
            _minioClient = minioClient;

            while (!_isConnected)
                InitializeRabbitMQ();

            MinioBucketHandler();
        }

        private void InitializeRabbitMQ()
        {

            if (_connection != null && _connection.IsOpen && _model != null && _model.IsOpen)
            {
                _isConnected = true;
                return;
            }
            else
            {
                //refatorar para outro lugar
                ConnectionFactory connectionFactory = new ConnectionFactory()
                {
                    HostName = "rabbitmq",  // Nome do serviço RabbitMQ definido no Docker Compose
                    Port = 5672 // Porta padrão do RabbitMQ para comunicação
                };


                _connection = connectionFactory.CreateConnection();
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

                _logger.LogInformation("RabbitMQ connection established, exchange and queue declared and bound.");
            }

        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {

                var consumer = new EventingBasicConsumer(_model);

                consumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    _logger.LogInformation($"Received message: {message}");

                    try
                    {
                        var request = JsonConvert.DeserializeObject<ImageProcessingRequest>(message);
                        await ProcessImageAsync(request);
                        _model.BasicAck(deliveryTag: ea.DeliveryTag, multiple: true);
                    }
                    catch (Exception ex)
                    {
                        _model.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                        _logger.LogError($"Error processing message: {message}. Error: {ex.Message}");

                    }

                };

                _model.BasicConsume(queue: QUEUE_NAME,
                                    autoAck: false,
                                    consumer: consumer);

                _logger.LogInformation("Started consuming RabbitMQ queue.");

                await Task.Delay(1000, stoppingToken);

            }

        }

        private async Task ProcessImageAsync(ImageProcessingRequest request)
        {
            var imageName = Path.GetFileName(request.ImageUrl);
            string imagePath = Path.Combine(Directory.GetCurrentDirectory(), imageName);


            System.IO.MemoryStream streamToReturn = new System.IO.MemoryStream();

            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_minioBucketName)
                .WithObject(imageName)
                .WithCallbackStream((stream) =>
                {
                    stream.CopyTo(streamToReturn);
                }));



            File.WriteAllBytes(imagePath, streamToReturn.ToArray());

            _logger.LogInformation($"Image downloaded from MinIO: {request.ImageUrl}");


            await _filterService.FilterHandler(imagePath, request.FilterType, request.ContentType);

            var processedImageName = $"processed_{request.FilterType}_{request.ImageUrl}";

            using var outputStream = new FileStream(imagePath, FileMode.Open);

            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_minioBucketName)
                .WithObject(processedImageName)
                .WithStreamData(outputStream)
                .WithObjectSize(outputStream.Length)
                .WithContentType(request.ContentType));

            _logger.LogInformation($"Processed image uploaded to MinIO: {processedImageName}");

            File.Delete(imagePath);

        }


        private async Task MinioBucketHandler()
        {
            //reformular em uma classe externa para aplicar o SOLID:
            var bktExistsArgs = new BucketExistsArgs().WithBucket(_minioBucketName);
            bool found = await _minioClient.BucketExistsAsync(bktExistsArgs);


            if (!found)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(_minioBucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs);
                Console.WriteLine(_minioBucketName + " created successfully");
            }
            else
            {
                Console.WriteLine(_minioBucketName + " already existis.");
            }
        }


        public override void Dispose()
        {
            _model.Close();
            _connection.Close();
            base.Dispose();
        }
    }


}
