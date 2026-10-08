using Auktionshuset.Application.Admin.Auctions.Bids;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.DeleteAuction;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Application.Admin.Employees.CreateEmployee;
using Auktionshuset.Application.Admin.Employees.DeleteEmployee;
using Auktionshuset.Application.Admin.Employees.UpdateEmployee;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.EventHandling;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class EventContractRegistry
    {
        //Skal måske i sin egen klasse? 
        internal sealed record EventContractRecord(
            string EventContractName,
            Type ClrType,
            Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>
                Dispatch);


        private readonly JsonSerializerOptions _jsonSerializerOptions = new();

        private readonly EventContractRecord[] _eventContracts =
        [
            EventContract<LotCreatedIntegrationEvent>("lot.created.v1"),
            EventContract<LotUpdatedIntegrationEvent>("lot.updated.v1"),
            EventContract<LotDeletedIntegrationEvent>("lot.deleted.v1"),

            EventContract<AuctionCreatedIntegrationEvent>("auction.created.v1"),
            EventContract<AuctionUpdatedIntegrationEvent>("auction.updated.v1"),
            EventContract<AuctionDeletedIntegrationEvent>("auction.deleted.v1"),

            EventContract<EmployeeCreatedIntegrationEvent>("employee.created.v1"),
            EventContract<EmployeeUpdatedIntegrationEvent>("employee.updated.v1"),
            EventContract<EmployeeDeletedIntegrationEvent>("employee.deleted.v1"),

            EventContract<BidPlacedIntegrationEvent>("bid.placed.v1")
        ];

        public IEnumerable<string> EventContractNames =>
            _eventContracts.Select(c => c.EventContractName);

        public EventContractRecord ByType(Type type)
        {
            return _eventContracts.SingleOrDefault(eventContract => eventContract.ClrType == type)
                   ?? throw new PermanentMessageException(
                       $"Event type '{type.Name}' not registered");
        }

        public EventContractRecord ByName(string name)
        {
            return _eventContracts.SingleOrDefault(eventContract =>
                       string.Equals(
                           eventContract.EventContractName,
                           name,
                           StringComparison.Ordinal)
                       || string.Equals(
                           eventContract.ClrType.AssemblyQualifiedName,
                           name,
                           StringComparison.Ordinal)
                       || name.StartsWith(
                           eventContract.ClrType.FullName + ",",
                           StringComparison.Ordinal))
                   ?? throw new PermanentMessageException(
                       $"Unknown Event Contract '{name}'.");
        }

        public string Serialize(IIntegrationEvent eventMessage)
        {
            if (eventMessage.EventId == Guid.Empty)
            {
                throw new PermanentMessageException(
                    "The event ID must be valid.");
            }

            EventContractRecord eventContract = ByType(eventMessage.GetType());

            return JsonSerializer.Serialize(
                eventMessage,
                eventContract.ClrType,
                _jsonSerializerOptions);
        }

        public IIntegrationEvent Deserialize(
            EventContractRecord eventContract,
            string payload)
        {
            try
            {
                var eventMessage = JsonSerializer.Deserialize(
                    payload,
                    eventContract.ClrType,
                    _jsonSerializerOptions) as IIntegrationEvent;

                if (eventMessage is null || eventMessage.EventId == Guid.Empty)
                {
                    throw new PermanentMessageException(
                        "The event ID must be valid.");
                }

                return eventMessage;
            }
            catch (JsonException exception)
            {
                throw new PermanentMessageException(
                    "Json for event invalid",
                    exception);
            }
        }

        private static EventContractRecord EventContract<TEvent>(
            string eventContractName)
            where TEvent : IIntegrationEvent
        {
            return new EventContractRecord(
                eventContractName,
                typeof(TEvent),
                async (serviceProvider, eventMessage, cancellationToken) =>
                {
                    IIntegrationEventHandler<TEvent>[] handlers = serviceProvider
                        .GetServices<IIntegrationEventHandler<TEvent>>()
                        .ToArray();

                    if (handlers.Length == 0)
                    {
                        throw new PermanentMessageException(
                            $"No handlers found for event type '{typeof(TEvent).Name}'.");
                    }

                    foreach (IIntegrationEventHandler<TEvent> handler in handlers)
                    {
                        await handler.HandleAsync(
                            (TEvent)eventMessage,
                            cancellationToken);
                    }
                });
        }
    }
}
