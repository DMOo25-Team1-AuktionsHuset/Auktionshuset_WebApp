namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class PermanentMessageException : Exception
    {
        public PermanentMessageException(string message)
            : base(message)
        {
        }

        public PermanentMessageException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
