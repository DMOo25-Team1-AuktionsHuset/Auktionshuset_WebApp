using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging
{
    public class OutboxPolicy
    {
        public const int MaxAttempts = 10;
        public const int LeaseSeconds = 60;
        public const int PublishTimeoutSeconds = 15;
        public const int MaxMessagesPerCycle = 50;

        public static readonly TimeSpan PollInterval =
            TimeSpan.FromSeconds(2);

        public static int RetryDelaySeconds(int attempt)
        {
            // 2, 4, 8, 16... maks 300 sekunder
            // TODO overvej lavere intervaltid (300 sek = 5 min)
            return Math.Min(300, 1 << Math.Min(attempt, 9));
        }
    }
}

