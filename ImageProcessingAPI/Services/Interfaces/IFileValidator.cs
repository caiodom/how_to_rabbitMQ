namespace ImageProcessingAPI.Services.Interfaces
{
    public interface IFileValidator
    {
        bool IsValid(IFormFile file);
    }
}

