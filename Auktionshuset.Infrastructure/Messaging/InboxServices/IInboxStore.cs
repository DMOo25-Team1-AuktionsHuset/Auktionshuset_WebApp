namespace Auktionshuset.Infrastructure.Messaging.InboxServices;

public interface IInboxStore
{
    Task<InboxTypes> StoreNewMessageAsync(
        string consumerName,
        string eventType,
        string? transportMessageId,
        byte[] payload,
        CancellationToken cancellationToken);
}
