using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Service.Vault{
    public sealed record RabbitMqCredentials(
        string Username,
        string Password);
}
