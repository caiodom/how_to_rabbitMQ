using ImageProcessingAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;


namespace ImageProcessingAPI.Controllers
{
    public class ImageProcessingController : ControllerBase
    {
        private readonly ILogger<ImageProcessingController> _logger;
        private readonly IImageProcessingService _imageProcessingService;

        public ImageProcessingController(IImageProcessingService imageProcessingService,
                                         ILogger<ImageProcessingController> logger)
        {
            _logger = logger;
            _imageProcessingService = imageProcessingService;
        }

        [HttpPost]
        [Route("process")]
        public async Task<IActionResult> ProcessImage(IFormFile image, string filterType)
        {
            try
            {
                if (image == null || string.IsNullOrEmpty(filterType))
                    return BadRequest("Invalid image or filter type.");

                var processedImageName =await _imageProcessingService.ProcessImage(image, filterType);

                return Ok(processedImageName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        [HttpGet]
        [Route("download")]
        public async Task<IActionResult> Download(string fileName)
        {
            try
            {
                var file=await _imageProcessingService.Download(fileName);

                if (!new FileExtensionContentTypeProvider().TryGetContentType(fileName, out var contentType))
                    contentType = "application/octet-stream";
                
                return File(file, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }





    }
}
