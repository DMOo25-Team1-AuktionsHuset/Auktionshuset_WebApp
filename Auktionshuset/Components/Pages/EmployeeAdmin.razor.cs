using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.DeleteEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;
using Auktionshuset.Domain;
using Auktionshuset.Models;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;

namespace Auktionshuset.Components.Pages;

public partial class EmployeeAdmin : IAsyncDisposable
{
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private static readonly CultureInfo DanishCulture = CultureInfo.GetCultureInfo("da-DK");

    [Inject] private EmployeeService EmployeeService { get; set; } = default!;
    [Inject] private EmployeeRealtimeService EmployeeRealtimeService { get; set; } = default!;

    private EmployeeFormModel model = new();
    private EditContext editContext = default!;
    private IReadOnlyList<EmployeeListItemResponse> employees = [];
    private bool isLoadingEmployees;
    private bool isSubmitting;
    private bool isDeleting;
    private bool submissionSucceeded;
    private bool isDisposed;
    private string? listError;
    private string? realtimeError;
    private string? statusMessage;
    private string searchTerm = string.Empty;
    private int pageSize = 10;
    private int page = 1;
    private Guid? editingEmployeeId;
    private Guid? editingAuctionHouseId;
    private Guid? pendingDeleteId;

    private IReadOnlyList<EmployeeListItemResponse> SortedEmployees => employees
        .OrderBy(employee => employee.FirstName, StringComparer.Create(DanishCulture, true))
        .ThenBy(employee => employee.LastName, StringComparer.Create(DanishCulture, true))
        .ToArray();

    private IReadOnlyList<EmployeeListItemResponse> FilteredEmployees
    {
        get
        {
            string term = searchTerm.Trim();
            IEnumerable<EmployeeListItemResponse> matching = SortedEmployees;
            if (term.Length > 0)
            {
                matching = matching.Where(employee =>
                    employee.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || employee.LastName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || $"{employee.FirstName} {employee.LastName}".Contains(term, StringComparison.OrdinalIgnoreCase)
                    || employee.Address.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            return matching.ToArray();
        }
    }

    private IReadOnlyList<EmployeeListItemResponse> PagedEmployees =>
        FilteredEmployees.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(FilteredEmployees.Count / (double)pageSize));

    protected override async Task OnInitializedAsync()
    {
        ResetForm();
        await LoadEmployeesAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        EmployeeRealtimeService.EmployeeCreated += OnEmployeeCreatedAsync;
        EmployeeRealtimeService.EmployeeUpdated += OnEmployeeUpdatedAsync;
        EmployeeRealtimeService.EmployeeDeleted += OnEmployeeDeletedAsync;
        try
        {
            await EmployeeRealtimeService.StartAsync();
        }
        catch (Exception)
        {
            realtimeError = "Liveforbindelsen til medarbejdere kunne ikke startes. Du kan stadig administrere medarbejdere; listen opdateres ved genindlæsning.";
            if (!isDisposed) StateHasChanged();
        }
    }

    private async Task SubmitAsync()
    {
        if (isSubmitting) return;
        isSubmitting = true;
        submissionSucceeded = false;
        statusMessage = null;
        try
        {
            if (editingEmployeeId is Guid employeeId)
            {
                await EmployeeService.UpdateAsync(employeeId, new UpdateEmployeeRequest
                {
                    FirstName = model.FirstName.Trim(),
                    LastName = model.LastName.Trim(),
                    BirthDate = model.BirthDate!.Value,
                    Address = model.Address.Trim(),
                    AuctionHouseId = editingAuctionHouseId ?? AuctionHouseDefaults.DefaultAuctionHouseId
                });
                statusMessage = "Medarbejderen blev opdateret.";
            }
            else
            {
                await EmployeeService.CreateAsync(new CreateEmployeeRequest
                {
                    FirstName = model.FirstName.Trim(),
                    LastName = model.LastName.Trim(),
                    BirthDate = model.BirthDate!.Value,
                    Address = model.Address.Trim(),
                    AuctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId
                });
                statusMessage = "Medarbejderen blev oprettet.";
            }

            await LoadEmployeesAsync();
            ResetForm();
            submissionSucceeded = true;
        }
        catch (EmployeeApiException exception)
        {
            statusMessage = exception.Message;
        }
        catch (HttpRequestException)
        {
            statusMessage = "Der kunne ikke oprettes forbindelse til serveren. Prøv igen om lidt.";
        }
        catch (TaskCanceledException)
        {
            statusMessage = "Anmodningen tog for lang tid. Prøv igen.";
        }
        finally
        {
            isSubmitting = false;
        }
    }

    private async Task LoadEmployeesAsync()
    {
        isLoadingEmployees = true;
        listError = null;
        try
        {
            employees = await EmployeeService.GetAllAsync();
            if (page > TotalPages) page = TotalPages;
        }
        catch (EmployeeApiException exception)
        {
            listError = exception.Message;
        }
        catch (HttpRequestException)
        {
            listError = "Medarbejderne kunne ikke hentes, fordi forbindelsen til serveren fejlede.";
        }
        catch (TaskCanceledException)
        {
            listError = "Hentning af medarbejdere tog for lang tid. Prøv igen.";
        }
        finally
        {
            isLoadingEmployees = false;
        }
    }

    private void BeginEdit(EmployeeListItemResponse employee)
    {
        editingEmployeeId = employee.EmployeeId;
        editingAuctionHouseId = employee.AuctionHouseId;
        model = new EmployeeFormModel
        {
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            BirthDate = employee.BirthDate,
            Address = employee.Address
        };
        editContext = new EditContext(model);
        pendingDeleteId = null;
        statusMessage = null;
        submissionSucceeded = false;
    }

    private void CancelEdit()
    {
        statusMessage = null;
        submissionSucceeded = false;
        ResetForm();
    }

    private void ResetForm()
    {
        editingEmployeeId = null;
        editingAuctionHouseId = null;
        model = new EmployeeFormModel();
        editContext = new EditContext(model);
    }

    private void RequestDelete(Guid employeeId)
    {
        pendingDeleteId = employeeId;
        statusMessage = null;
    }

    private void CancelDelete() => pendingDeleteId = null;

    private async Task ConfirmDeleteAsync(EmployeeListItemResponse employee)
    {
        if (isDeleting) return;
        isDeleting = true;
        try
        {
            await EmployeeService.DeleteAsync(employee.EmployeeId);
            if (editingEmployeeId == employee.EmployeeId) ResetForm();
            pendingDeleteId = null;
            statusMessage = $"Medarbejderen {employee.FirstName} {employee.LastName} blev slettet.";
            submissionSucceeded = true;
            await LoadEmployeesAsync();
        }
        catch (EmployeeApiException exception)
        {
            listError = exception.Message;
        }
        catch (HttpRequestException)
        {
            listError = "Medarbejderen kunne ikke slettes, fordi forbindelsen til serveren fejlede.";
        }
        catch (TaskCanceledException)
        {
            listError = "Sletningen tog for lang tid. Prøv igen.";
        }
        finally
        {
            isDeleting = false;
        }
    }

    private void OnRowKeyDown(KeyboardEventArgs args, EmployeeListItemResponse employee)
    {
        if (args.Key is "Enter" or " ") BeginEdit(employee);
    }

    private void OnSearchChanged() => page = 1;
    private void OnPageSizeChanged() => page = 1;
    private void PreviousPage() { if (page > 1) page--; }
    private void NextPage() { if (page < TotalPages) page++; }

    private Task OnEmployeeCreatedAsync(CreateEmployeeNotification _) => RefreshEmployeesAsync();
    private Task OnEmployeeUpdatedAsync(UpdateEmployeeNotification _) => RefreshEmployeesAsync();
    private Task OnEmployeeDeletedAsync(DeleteEmployeeNotification _) => RefreshEmployeesAsync();

    private Task RefreshEmployeesAsync()
    {
        if (isDisposed) return Task.CompletedTask;
        return InvokeAsync(async () =>
        {
            if (isDisposed) return;
            await LoadEmployeesAsync();
            if (!isDisposed) StateHasChanged();
        });
    }

    public async ValueTask DisposeAsync()
    {
        isDisposed = true;
        EmployeeRealtimeService.EmployeeCreated -= OnEmployeeCreatedAsync;
        EmployeeRealtimeService.EmployeeUpdated -= OnEmployeeUpdatedAsync;
        EmployeeRealtimeService.EmployeeDeleted -= OnEmployeeDeletedAsync;
        await Task.CompletedTask;
    }
}
