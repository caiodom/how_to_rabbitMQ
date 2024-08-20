using Core.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Minio;
using Minio.ApiEndpoints;
using Minio.DataModel;
using Minio.DataModel.Args;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Security.AccessControl;
using System.Text;

namespace ImageProcessingAPI.Controllers
{
    public class ImageProcessingController : ControllerBase
    {
        private readonly ILogger<ImageProcessingController> _logger;
        private IMinioClient _minioClient;
        private const string BucketName = "minhas-imagens";
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private const string ExchangeName = "image_processing_exchange";
        private const string RoutingKey = "image.process";
        private const string MINIO_PROCCESSED_IMAGES = "processed-images";

        public ImageProcessingController(IMinioClient minioClient, ILogger<ImageProcessingController> logger)
        {
            _logger = logger;

            // Configura o RabbitMQ
            var factory = new ConnectionFactory() { HostName = "rabbitmq" }; // Use o nome do serviço RabbitMQ definido no Docker Compose
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declara a exchange e a fila, caso ainda não existam
            _channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Direct);


            _minioClient = minioClient;
            //InitializeMinioClient();
        }

        [HttpPost]
        [Route("process")]
        public async Task<IActionResult> ProcessImage(IFormFile image, string filterType)
        {
            if (image == null || string.IsNullOrEmpty(filterType))
            {
                return BadRequest("Invalid image or filter type.");
            }

            string imageName = image.FileName;

            try
            {

                await MinioBucketHandler();

                // Faz o upload da imagem diretamente do IFormFile para o MinIO
                using (var stream = image.OpenReadStream())
                {
                    await _minioClient.PutObjectAsync(new PutObjectArgs()
                        .WithBucket(MINIO_PROCCESSED_IMAGES)
                        .WithObject(imageName)
                        .WithStreamData(stream)
                        .WithObjectSize(image.Length)
                        .WithContentType(image.ContentType));
                }

                // Cria a mensagem para o RabbitMQ com o caminho da imagem no MinIO e o tipo de filtro
                var message = new ImageProcessingRequest
                {
                    ImageUrl = imageName,
                    FilterType = filterType,
                    ContentType=image.ContentType
                };

                var buckets = await _minioClient.ListBucketsAsync();

                var messageBody = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));

                // Publica a mensagem no RabbitMQ
                _channel.BasicPublish(exchange: ExchangeName, routingKey: RoutingKey, basicProperties: null, body: messageBody);

                var processedImageName = $"processed_{filterType}_{imageName}";
                return Ok(processedImageName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        [HttpGet]
        [Route("download")]
        public async Task<IActionResult> Download(string fileName)
        {

            try
            {

                System.IO.MemoryStream streamToReturn = new System.IO.MemoryStream();

                await _minioClient.GetObjectAsync(new GetObjectArgs()
                  .WithBucket(MINIO_PROCCESSED_IMAGES)
                 .WithObject(fileName)
                 .WithCallbackStream((stream) =>
                 {
                     stream.CopyTo(streamToReturn);
                 }));

                if (!new FileExtensionContentTypeProvider().TryGetContentType(fileName, out var contentType))
                    contentType = "application/octet-stream";

                return File(streamToReturn.ToArray(), contentType,fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }

        }

        private async Task MinioBucketHandler()
        {
            //reformular em uma classe externa para aplicar o SOLID:
            var bktExistsArgs = new BucketExistsArgs().WithBucket(BucketName);
            bool found = await _minioClient.BucketExistsAsync(bktExistsArgs);


            if (!found)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(BucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs);
                Console.WriteLine(BucketName + " created successfully");
            }
            else
            {
                Console.WriteLine(BucketName + " already existis.");
            }
        }





    }
}
