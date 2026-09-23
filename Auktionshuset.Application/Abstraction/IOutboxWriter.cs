using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Abstraction
{
    public interface IOutboxWriter
    {
        Task AddAsync(
            IIntegrationEvent integrationEvent,
            CancellationToken cancellationToken = default);
    }
}
