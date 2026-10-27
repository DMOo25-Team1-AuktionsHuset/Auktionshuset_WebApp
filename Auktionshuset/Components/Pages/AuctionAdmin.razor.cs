using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Security;
using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.DeleteEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Models;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Authorization;
using System.Globalization;
using System.Security.Claims;
using Auktionshuset.Contracts.Time;

namespace Auktionshuset.Components.Pages;

public partial class AuctionAdmin : IDisposable
{
    /// <summary>
    /// The value the status filter uses to mean "no filtering".
    /// </summary>
    private const string AllStatusesFilter = "all";

    // The same statuses the API derives, kept here so the form can preview the status of a draft.
    private const string StatusUpcoming = "Kommende";
    private const string StatusLive = "Live";
    private const string StatusEnded = "Afsluttet";

    private static readonly int[] PageSizeOptions = [10, 25, 50];

    private static readonly CultureInfo DanishCulture = CultureInfo.GetCultureInfo("da-DK");

    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    [Inject]
    private LotService LotService { get; set; } = default!;

    [Inject]
    private AuctionService AuctionService { get; set; } = default!;

    [Inject]
    private EmployeeService EmployeeService { get; set; } = default!;

    [Inject]
    private AuctionRealtimeService AuctionRealtimeService { get; set; } = default!;

    [Inject]
    private TimeProvider Clock { get; set; } = default!;


    // --- Formular ---------------------------------------------------------

    private CreateAuctionFormModel model = new();
    private EditContext editContext = default!;
    private readonly Dictionary<Guid, SelectedLot> selectedLots = [];

    private Guid? editingAuctionId;
    private bool isSubmitting;
    private bool submissionSucceeded;
    private string? statusMessage;

    // --- Genstande --------------------------------------------------------

    private IReadOnlyList<LotListItemResponse> lots = [];
    private bool isLoadingLots;
    private string? lotListError;
    private string pickerSearchTerm = string.Empty;

    // --- Medarbejdere -----------------------------------------------------

    private IReadOnlyList<EmployeeOptionResponse> employees = [];
    private bool isLoadingEmployees;
    private string? employeeError;

    // --- Auktionsoversigt -------------------------------------------------

    private IReadOnlyList<AuctionListItemResponse> auctions = [];
    private bool isLoadingAuctions;
    private string? auctionListError;
    private string? realtimeError;
    private string auctionSearchTerm = string.Empty;
    private string statusFilter = AllStatusesFilter;
    private int auctionPageSize = 10;
    private int auctionPage = 1;
    private Guid? pendingDeleteId;
    private bool isDeleting;
    private bool isDisposed;
    private bool canViewLots;
    private bool canCreateAuctions;
    private bool canUpdateAuctions;
    private bool canDeleteAuctions;
    private int auctionLoadVersion;

    /// <summary>
    /// One genstand chosen for the auction, with the number of units the auction includes.
    /// </summary>
    private sealed record SelectedLot(string Name, int Quantity, int? Available);

    private IReadOnlyList<LotListItemResponse> FilteredPickerLots
    {
        get
        {
            string term = pickerSearchTerm.Trim();

            IEnumerable<LotListItemResponse> matching = term.Length == 0
                ? lots.AsEnumerable()
                : lots.Where(lot =>
                    lot.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || lot.Category.Contains(term, StringComparison.OrdinalIgnoreCase));

            // Valgte genstande står øverst, og begge grupper sorteres alfabetisk på navn.
            return matching
                .OrderByDescending(lot => selectedLots.ContainsKey(lot.LotId))
                .ThenBy(lot => lot.Name, StringComparer.Create(DanishCulture, ignoreCase: true))
                .ToArray();
        }
    }

    private bool HasPickerSearch => pickerSearchTerm.Trim().Length > 0;

    private IReadOnlyList<SelectedLot> SelectedLotLines =>
        selectedLots.Values
            .OrderBy(line => line.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    private int SelectedItemCount => selectedLots.Values.Sum(line => line.Quantity);

    /// <summary>
    /// Gets the status the form values currently imply, so the draft can be previewed before saving.
    /// </summary>
    private string DerivedStatus =>
        model.GetStartsAt() is null
            ? "Ikke fastsat"
            : StatusUpcoming;
    
    
    private IReadOnlyList<AuctionListItemResponse> FilteredAuctions
    {
        get
        {
            string term = auctionSearchTerm.Trim();
            IEnumerable<AuctionListItemResponse> filtered = auctions.AsEnumerable();

            if (statusFilter != AllStatusesFilter)
            {
                filtered = filtered.Where(auction => auction.Status == statusFilter);
            }

            if (term.Length > 0)
            {
                filtered = filtered.Where(auction =>
                    auction.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || auction.AuctionId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)
                    || auction.EmployeeName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            return filtered.ToArray();
        }
    }

    private IReadOnlyList<AuctionListItemResponse> PagedAuctions =>
        FilteredAuctions.Skip((auctionPage - 1) * auctionPageSize).Take(auctionPageSize).ToArray();

    private int TotalAuctionPages =>
        Math.Max(1, (int)Math.Ceiling(FilteredAuctions.Count / (double)auctionPageSize));

    protected override async Task OnInitializedAsync()
    {
        ClaimsPrincipal user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
        canViewLots = user.HasClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.ViewLotsPermission);
        canCreateAuctions = user.HasClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.CreateAuctionPermission);
        canUpdateAuctions = user.HasClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.UpdateAuctionPermission);
        canDeleteAuctions = user.HasClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.DeleteAuctionPermission);
        ResetForm();

        await Task.WhenAll(LoadLotsAsync(), LoadEmployeesAsync(), LoadAuctionsAsync());
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        AuctionRealtimeService.Invalidated += RefreshAuctionsAsync;
        AuctionRealtimeService.ConnectionChanged += OnAuctionConnectionChangedAsync;

        try
        {
            await AuctionRealtimeService.StartAsync();

            await OnAuctionConnectionChangedAsync();

            await RefreshAuctionsAsync();
        }
        catch (Exception)
        {
            if(!isDisposed)
            {
                realtimeError = "Liveforbindelsen kunne ikke startes.";
                StateHasChanged();
            }
        }
    }

    public void Dispose()
    {
        isDisposed = true;

        AuctionRealtimeService.Invalidated -= RefreshAuctionsAsync;
        AuctionRealtimeService.ConnectionChanged -= OnAuctionConnectionChangedAsync;
    }

    private Task RefreshAuctionsAsync()
    {
        if (isDisposed)
        {
            return Task.CompletedTask;
        }

        return InvokeAsync(async () =>
        {
            if (isDisposed)
                return;

            await LoadAuctionsAsync();

            if (!isDisposed)
                StateHasChanged();
        });
    }

    private Task OnAuctionConnectionChangedAsync()
    {
        if (isDisposed)
            return Task.CompletedTask;

        return InvokeAsync(() =>
        {
            if (isDisposed)
                return;

            realtimeError = AuctionRealtimeService.isConnected
                ? null
                : "Liveforbindelsen er ikke tilsluttet, listen opdateres ikke automatisk";

            StateHasChanged();
        });
    }

    // --- Opret og redigér -------------------------------------------------

    /// <summary>
    /// Creates or updates the auction described by the form and reloads the dashboard.
    /// </summary>
    private async Task SubmitAsync()
    {
        if (isSubmitting
            || (editingAuctionId.HasValue ? !canUpdateAuctions : !canCreateAuctions))
        {
            return;
        }

        string? quantityError = ValidateQuantities();

        if (quantityError is not null)
        {
            statusMessage = quantityError;
            submissionSucceeded = false;
            return;
        }

        DateTime? startsAt = model.GetStartsAt();
        DateTime? endsAt = model.GetEndsAt();

        if (startsAt is null || (!string.IsNullOrWhiteSpace(model.EndTime) && endsAt is null))
        {
            statusMessage = "Udfyld en gyldig startdato og starttid før auktionen gemmes.";
            submissionSucceeded = false;
            return;
        }

        if (!CopenhagenTime.TryToUtc(startsAt.Value, out DateTime startsAtUtc, out string? conversionError))
        {
            statusMessage = conversionError;
            submissionSucceeded = false;
            return;
        }

        DateTime? endsAtUtc = null;

        if (endsAt is DateTime localEnd)
        {
            if (!CopenhagenTime.TryToUtc(
                localEnd,
                out DateTime convertedEnd,
                out conversionError))
            {
                statusMessage = conversionError;
                submissionSucceeded = false;
                return;
            }

            endsAtUtc = convertedEnd;
        }

        isSubmitting = true;
        submissionSucceeded = false;
        statusMessage = null;

        try
        {
            AuctionLotRequest[] requestLots = selectedLots
                .Select(entry => new AuctionLotRequest(entry.Key, entry.Value.Quantity))
                .ToArray();

            string successMessage;

            if (editingAuctionId is { } auctionId)
            {
                await AuctionService.UpdateAsync(auctionId, new UpdateAuctionRequest
                {
                    Name = model.Name.Trim(),
                    StartsAt = new DateTimeOffset(startsAtUtc),
                    EndsAt = endsAtUtc is DateTime endUtc ? new DateTimeOffset(endUtc) : null,
                    EmployeeId = model.EmployeeId,
                    Lots = requestLots
                });

                successMessage = "Auktionen blev gemt.";
            }
            else
            {
                CreateAuctionResponse response = await AuctionService.CreateAsync(new CreateAuctionRequest
                {
                    Name = model.Name.Trim(),
                    StartsAt = new DateTimeOffset(startsAtUtc),
                    EndsAt = endsAtUtc is DateTime endUtc ? new DateTimeOffset(endUtc) : null,
                    EmployeeId = model.EmployeeId,
                    Lots = requestLots
                });

                successMessage = response.LotCount == 0
                    ? "Auktionen blev oprettet uden genstande."
                    : $"Auktionen blev oprettet med {response.LotCount} genstand{(response.LotCount == 1 ? string.Empty : "e")} ({response.ItemCount} enhed{(response.ItemCount == 1 ? string.Empty : "er")}).";
            }

            await LoadAuctionsAsync();
            ResetForm();

            submissionSucceeded = true;
            statusMessage = successMessage;
        }
        catch (AuctionApiException exception)
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

    /// <summary>
    /// Loads an auction into the form and switches the page into edit mode.
    /// </summary>
    /// <param name="auction">The dashboard row that was activated.</param>
    private async Task BeginEditAsync(AuctionListItemResponse auction)
    {
        if (!canUpdateAuctions)
        {
            return;
        }

        try
        {
            AuctionDetailResponse? detail = await AuctionService.GetByIdAsync(auction.AuctionId);

            if (detail is null)
            {
                statusMessage = "Auktionen findes ikke længere. Listen er opdateret.";
                submissionSucceeded = false;
                await LoadAuctionsAsync();
                return;
            }

            DateTime localStart = CopenhagenTime.FromUtc(detail.StartsAt);

            DateTime? localEnd = detail.EndsAt is DateTime endUtc
                ? CopenhagenTime.FromUtc(endUtc)
                : null;

            editingAuctionId = detail.AuctionId;

            model = new CreateAuctionFormModel
            {
                Name = detail.Name,
                StartDate = localStart.Date,
                StartTime = localStart.ToString("HH:mm", CultureInfo.InvariantCulture),
                EndTime = localEnd?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty,
                EmployeeId = detail.EmployeeId,
                RequireFutureStart = detail.StartsAt > Clock.GetUtcNow().UtcDateTime
            };

            editContext = new EditContext(model);

            selectedLots.Clear();

            foreach (AuctionLotResponse line in detail.Lots)
            {
                int? available = lots.FirstOrDefault(lot => lot.LotId == line.LotId)?.Quantity;
                selectedLots[line.LotId] = new SelectedLot(line.Name, line.Quantity, available);
            }

            pendingDeleteId = null;
            statusMessage = null;
            submissionSucceeded = false;
        }
        catch (AuctionApiException exception)
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
    }

    /// <summary>
    /// Leaves edit mode and clears the form and its selection.
    /// </summary>
    private void CancelEdit()
    {
        statusMessage = null;
        submissionSucceeded = false;

        ResetForm();
    }

    private void ResetForm()
    {
        editingAuctionId = null;
        pendingDeleteId = null;
        selectedLots.Clear();

        model = new CreateAuctionFormModel();
        editContext = new EditContext(model);
    }

    // --- Valg af genstande ------------------------------------------------

    private void ToggleLot(LotListItemResponse lot, ChangeEventArgs args)
    {
        if (args.Value is true)
        {
            // Hele genstandslinjen tilføjes, så antallet følger genstandens lagerantal.
            selectedLots[lot.LotId] = new SelectedLot(lot.Name, lot.Quantity, lot.Quantity);
        }
        else
        {
            selectedLots.Remove(lot.LotId);
        }
    }

    private void ClearSelection() => selectedLots.Clear();

    /// <summary>
    /// Checks the chosen quantities against the stock of each genstand before the API is called.
    /// </summary>
    /// <returns>A Danish error message, or <see langword="null"/> when every quantity is valid.</returns>
    private string? ValidateQuantities()
    {
        foreach ((Guid lotId, SelectedLot? line) in selectedLots)
        {
            if (line.Quantity < 1)
            {
                return $"Antallet for \"{line.Name}\" skal være mindst 1.";
            }

            int? available = line.Available
                ?? lots.FirstOrDefault(lot => lot.LotId == lotId)?.Quantity;

            if (available is { } stock && line.Quantity > stock)
            {
                return $"Der er kun {stock} stk. af \"{line.Name}\" på lageret.";
            }
        }

        return null;
    }

    // --- Auktionsoversigt -------------------------------------------------

    private async Task LoadAuctionsAsync()
    {
        if (isDisposed)
            return;

        int version = ++auctionLoadVersion;

        isLoadingAuctions = true;
        auctionListError = null;

        try
        {
            var result = await AuctionService.GetAllAsync();

            if (isDisposed || version != auctionLoadVersion)
                return;

            auctions = result;
            auctionPage = Math.Clamp(
                auctionPage,
                1,
                TotalAuctionPages);
        }
        catch (Exception ex) when (ex
            is AuctionApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            if (isDisposed || version != auctionLoadVersion)
                return;

            auctionListError = ex switch
            {
                AuctionApiException apiException =>
                    apiException.Message,

                TaskCanceledException =>
                    "Hentning af auktioner to for lang tid",

                _ => "Auktioner kunne ikke hentes, prøv igen"
            };
        }
        finally
        {
            if (!isDisposed && version == auctionLoadVersion)
                isLoadingAuctions = false;
        }
    }

    private void OnAuctionSearchChanged() => auctionPage = 1;

    private void OnAuctionPageSizeChanged() => auctionPage = 1;

    private void GoToPreviousAuctionPage()
    {
        if (auctionPage > 1)
        {
            auctionPage--;
        }
    }

    private void GoToNextAuctionPage()
    {
        if (auctionPage < TotalAuctionPages)
        {
            auctionPage++;
        }
    }

    private void RequestDelete(Guid auctionId)
    {
        if (!canDeleteAuctions)
        {
            return;
        }

        pendingDeleteId = auctionId;
        statusMessage = null;
    }

    private void CancelDelete() => pendingDeleteId = null;

    /// <summary>
    /// Deletes the auction awaiting confirmation and reloads the dashboard.
    /// </summary>
    private async Task ConfirmDeleteAsync()
    {
        if (pendingDeleteId is not { } auctionId || isDeleting || isSubmitting || !canDeleteAuctions)
        {
            return;
        }

        isDeleting = true;
        submissionSucceeded = false;
        statusMessage = null;

        try
        {
            await AuctionService.DeleteAsync(auctionId);

            if (editingAuctionId == auctionId)
            {
                ResetForm();
            }
            else
            {
                pendingDeleteId = null;
            }

            await LoadAuctionsAsync();

            submissionSucceeded = true;
            statusMessage = "Auktionen blev slettet.";
        }
        catch (AuctionApiException exception)
        {
            pendingDeleteId = null;
            statusMessage = exception.Message;
        }
        catch (HttpRequestException)
        {
            statusMessage = "Der kunne ikke oprettes forbindelse til serveren. Prøv igen om lidt.";
        }
        catch (TaskCanceledException)
        {
            statusMessage = "Sletningen tog for lang tid. Prøv igen.";
        }
        finally
        {
            isDeleting = false;
        }
    }

    // --- Data -------------------------------------------------------------

    private async Task LoadLotsAsync()
    {
        if (!canViewLots)
        {
            lots = [];
            isLoadingLots = false;
            lotListError = "Du har ikke rettighed til at se lageret. Auktionen kan stadig oprettes uden genstande.";
            return;
        }

        isLoadingLots = true;
        lotListError = null;

        try
        {
            lots = await LotService.GetAllAsync();
        }
        catch (LotApiException exception)
        {
            lotListError = exception.Message;
        }
        catch (HttpRequestException)
        {
            lotListError = "Listen over genstande kunne ikke hentes. Prøv igen om lidt.";
        }
        catch (TaskCanceledException)
        {
            lotListError = "Anmodningen om listen over genstande tog for lang tid. Prøv igen.";
        }
        finally
        {
            isLoadingLots = false;
        }
    }

    private async Task LoadEmployeesAsync()
    {
        isLoadingEmployees = true;
        employeeError = null;

        try
        {
            employees = await EmployeeService.GetOptionsAsync();
        }
        catch (EmployeeApiException exception)
        {
            employeeError = exception.Message;
        }
        catch (HttpRequestException)
        {
            employeeError = "Medarbejderne kunne ikke hentes. Prøv igen om lidt.";
        }
        catch (TaskCanceledException)
        {
            employeeError = "Anmodningen om medarbejderne tog for lang tid. Prøv igen.";
        }
        finally
        {
            isLoadingEmployees = false;
        }
    }

    // --- Hjælpere ---------------------------------------------------------

    private static string ShortId(Guid id) => id.ToString("N")[..8];

    private static string StatusCssClass(string status) => status switch
    {
        StatusUpcoming => "badge-upcoming",
        StatusLive => "badge-live",
        _ => "badge-ended"
    };
}
