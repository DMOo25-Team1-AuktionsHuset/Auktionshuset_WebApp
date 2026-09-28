using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.DeleteEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;
using Microsoft.AspNetCore.SignalR.Client;

namespace Auktionshuset.Services;

public sealed class EmployeeRealtimeService : IAsyncDisposable
{
    private readonly string hubUrl;
    private readonly SemaphoreSlim startGate = new(1, 1);
    private HubConnection? connection;

    public EmployeeRealtimeService(IConfiguration configuration)
    {
        string apiBaseUrl = configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Configuration value 'Api:BaseUrl' is required.");
        hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/employee";
    }

    public event Func<CreateEmployeeNotification, Task>? EmployeeCreated;
    public event Func<UpdateEmployeeNotification, Task>? EmployeeUpdated;
    public event Func<DeleteEmployeeNotification, Task>? EmployeeDeleted;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await startGate.WaitAsync(cancellationToken);
        try
        {
            if (connection is not null)
            {
                return;
            }

            HubConnection hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            hubConnection.On<CreateEmployeeNotification>(nameof(IEmployeeClient.EmployeeCreatedAsync),
                notification => EmployeeCreated?.Invoke(notification) ?? Task.CompletedTask);
            hubConnection.On<UpdateEmployeeNotification>(nameof(IEmployeeClient.EmployeeUpdatedAsync),
                notification => EmployeeUpdated?.Invoke(notification) ?? Task.CompletedTask);
            hubConnection.On<DeleteEmployeeNotification>(nameof(IEmployeeClient.EmployeeDeletedAsync),
                notification => EmployeeDeleted?.Invoke(notification) ?? Task.CompletedTask);

            try
            {
                await hubConnection.StartAsync(cancellationToken);
            }
            catch
            {
                await hubConnection.DisposeAsync();
                throw;
            }

            connection = hubConnection;
        }
        finally
        {
            startGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
            connection = null;
        }

        startGate.Dispose();
    }
}
