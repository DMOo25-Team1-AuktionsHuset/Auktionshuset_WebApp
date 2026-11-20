using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Messaging;

namespace Auktionshuset.Infrastructure.Test;

public sealed class DeviceEventContractTests
{
    [Fact]
    public void DeviceContractsRoundTripWithoutCredentials()
    {
        var registry = new EventContractRegistry();
        var now = DateTime.UtcNow;
        IIntegrationEvent[] events =
        [
            new DeviceRegisteredIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, now),
            new DeviceIssuedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now),
            new DeviceReleasedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now)
        ];
        string[] names = ["device.registered.v1", "device.issued.v1", "device.released.v1"];
        for (var i = 0; i < events.Length; i++)
        {
            var contract = registry.ByType(events[i].GetType());
            Assert.Equal(names[i], contract.EventContractName);
            var payload = registry.Serialize(events[i]);
            Assert.DoesNotContain("secret", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hash", payload, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(events[i], registry.Deserialize(registry.ByName(names[i]), payload));
        }
    }
}
