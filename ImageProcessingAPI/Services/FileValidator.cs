using ImageProcessingAPI.Services.Interfaces;

namespace ImageProcessingAPI.Services
{
    public class FileValidator : IFileValidator
    {
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".png", ".jpeg", ".jpg", ".gif", ".bmp", ".pbm", ".tga", ".tiff", ".tif", ".webp", ".qoi"
                };

        public bool IsValid(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            return !string.IsNullOrEmpty(extension) && AllowedExtensions.Contains(extension);
        }
    }
}
