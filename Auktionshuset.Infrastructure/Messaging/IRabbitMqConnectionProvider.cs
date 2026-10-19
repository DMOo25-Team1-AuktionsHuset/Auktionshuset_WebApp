using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal interface IRabbitMqConnectionProvider
    {
        Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken);
    }
}
