using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Contracts
{
    public class RabbitMQProcessSettings
    {
        public string ExchangeName { get; set; }
        public string RoutingKey { get; set; }
        public string QueueName { get; set; }
    }
}
