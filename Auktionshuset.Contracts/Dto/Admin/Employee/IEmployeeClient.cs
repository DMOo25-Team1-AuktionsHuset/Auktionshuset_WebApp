using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.DeleteEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;

namespace Auktionshuset.Contracts.Dto.Admin.Employee
{
    public interface IEmployeeClient
    {
        //  Task EmployeeCreatedAsync(CreateEmployeeNotification notification);
        Task EmployeeCreatedAsync(CreateEmployeeNotification notification);
        // Task EmployeeUpdatedAsync(UpdateEmployeeNotification notification);
        Task EmployeeUpdatedAsync(UpdateEmployeeNotification notification);
        // Task EmployeeUpdatedAsync(DeleteEmployeeNotification notification);
        Task EmployeeDeletedAsync(DeleteEmployeeNotification notification);
    }
}
