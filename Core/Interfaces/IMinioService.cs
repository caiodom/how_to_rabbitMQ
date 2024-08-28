namespace Core.Interfaces
{
    public interface IMinioService
    {
        Task GetObjectAsync(MemoryStream memoryStream, string bucketName, string objectName);

        Task<string> PutObjectAsync(string bucketName,
                                            string processedImageName,
                                            FileStream outputStream,
                                            string contentType);


        Task<string> PutObjectAsync(string bucketName,
                                            string processedImageName,
                                            Stream outputStream,
                                            string contentType);


    }
}
