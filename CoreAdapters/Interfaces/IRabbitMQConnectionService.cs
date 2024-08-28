using RabbitMQ.Client;


namespace CoreAdapters.Interfaces.Configuration
{
    public interface IRabbitMQConnectionService: IDisposable
    {
        IConnection GetConnection();
    }
}
