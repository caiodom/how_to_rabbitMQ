using CoreAdapters.Interfaces.Configuration;
using Core.Contracts;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;


namespace CoreAdapters.Configuration
{
    public class RabbitMQConnectionService : IRabbitMQConnectionService
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly RabbitMQSettings _settings;
        private readonly int _retryCount = 8;
        private IConnection _connection;

        public RabbitMQConnectionService(RabbitMQSettings settings)
        {
            _settings = settings;

            _connectionFactory = new ConnectionFactory()
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                DispatchConsumersAsync = true
            };

            Connect();
        }

        public IConnection GetConnection()
        {

            if (_connection == null || !_connection.IsOpen)
                throw new InvalidOperationException("Conexão não esta aberta.");

            return _connection;
        }

        private void Connect()
        {
            Policy
            .Handle<BrokerUnreachableException>()
            .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"Tentativa de reconexão {retryCount} falhou. Tentando novamente em {timeSpan.TotalSeconds} segundos.");
                }).Execute(() =>
            {
                _connection = _connectionFactory.CreateConnection();
                Console.Write("Conexão estabelecida com sucesso!");
            });
        }




    }
}
