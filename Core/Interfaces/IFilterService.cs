namespace Core.Interfaces
{
    public interface IFilterService
    {
        Task FilterHandler(string imagePath, string filterType,string contentType);
        Task FilterHandler(MemoryStream stream, string filterType,string contentType);

    }
}
