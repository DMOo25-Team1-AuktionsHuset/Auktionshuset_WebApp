using Auktionshuset.Contracts.Dto.Admin.Employee;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Hubs
{
    public class EmployeeHub : Hub<IEmployeeClient>
    {
    }
}
