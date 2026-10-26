using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities
{
    public sealed class DeviceCredential
    {

        public required Guid DeviceId { get; set; }
        public Device Device { get; set; } = null!;
        public required string SecretHash { get; set; }
        public bool IsEnabled { get; set; } = false;
        public int CredentialVersion { get; set; } = 1;

    }
}
