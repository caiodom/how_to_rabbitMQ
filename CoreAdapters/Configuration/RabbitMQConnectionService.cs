using CoreAdapters.Interfaces.Configuration;
using Core.Contracts;
using Microsoft.VisualBasic;
using Polly;
using Polly.Retry;
using Polly.Wrap;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace CoreAdapters.Configuration
{
    public class RabbitMQConnectionService : IRabbitMQConnectionService
    {
        private IConnectionFactory _connectionFactory;
        private readonly RabbitMQSettings _settings;
        private int _retryCount = 8;
        private IConnection _connection;

        public RabbitMQConnectionService(RabbitMQSettings settings)
        {
            _settings = settings;
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
            _connectionFactory = new ConnectionFactory()
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                DispatchConsumersAsync=true
            };


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
