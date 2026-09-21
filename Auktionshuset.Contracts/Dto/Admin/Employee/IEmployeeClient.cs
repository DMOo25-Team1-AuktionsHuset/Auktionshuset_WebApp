using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.DeleteEmployee;
// using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
// using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Employee
{
    public interface IEmployeeClient
    {
        //  Task EmployeeCreatedAsync(CreateEmployeeNotification notification);
        Task EmployeeCreatedAsync(CreateEmployeeNotification notification);
        // Task EmployeeUpdatedAsync(UpdateEmployeeNotification notification);
        Task EmployeeDeletedAsync(DeleteEmployeeNotification notification);
    }
}
