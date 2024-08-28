using RabbitMQ.Client;


namespace CoreAdapters.Interfaces.Configuration
{
    public interface IRabbitMQConnectionService
    {
        IConnection GetConnection();
    }
}
