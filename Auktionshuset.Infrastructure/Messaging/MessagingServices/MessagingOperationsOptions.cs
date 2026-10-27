using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging.MessagingServices
{
    internal class MessagingOperationsOptions
    {
        public bool RunWorkers { get; set; } = true;
        public bool RunMaintenance { get; set; } = true;

        public int OutboxRetentionDays { get; set; } = 30;
        public int CleanupBatchSize { get; set; } = 1000;
        public int MaintenanceIntervalSeconds { get; set; } = 60;

        public int StatusSampleIntervalSeconds { get; set; } = 15;
        public int WorkerStaleAfterSeconds { get; set; } = 120;
        public int BacklogWarningAfterSeconds { get; set; } = 300;
        public int StartupGraceSeconds { get; set; } = 30;
    }
}
