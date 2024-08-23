namespace ImageProcessingAPI.Services.Interfaces
{
    public interface IImageProcessingService
    {
        Task<string> ProcessImage(IFormFile image, string filterType);
        Task<byte[]> Download(string fileName);
    }
}
