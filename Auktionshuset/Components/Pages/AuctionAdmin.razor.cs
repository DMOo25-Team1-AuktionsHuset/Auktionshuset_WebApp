using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Models;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Globalization;

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
    private LotService LotService { get; set; } = default!;

    [Inject]
    private AuctionService AuctionService { get; set; } = default!;

    [Inject]
    private EmployeeService EmployeeService { get; set; } = default!;

    [Inject]
    private AuctionRealtimeService AuctionRealtimeService { get; set; } = default!;

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

    private IReadOnlyList<EmployeeListItemResponse> employees = [];
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
    private string DerivedStatus
    {
        get
        {
            DateTime? startsAt = model.GetStartsAt();
            DateTime? endsAt = model.GetEndsAt();

            if (startsAt is null || endsAt is null)
            {
                return "Ikke fastsat";
            }

            return DeriveStatus(startsAt.Value, endsAt.Value);
        }
    }

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
        ResetForm();

        await Task.WhenAll(LoadLotsAsync(), LoadEmployeesAsync(), LoadAuctionsAsync());
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        AuctionRealtimeService.AuctionCreated += OnAuctionCreatedAsync;
        AuctionRealtimeService.AuctionUpdated += OnAuctionUpdatedAsync;
        AuctionRealtimeService.AuctionDeleted += OnAuctionDeletedAsync;

        try
        {
            await AuctionRealtimeService.StartAsync();
        }
        catch (Exception)
        {
            realtimeError = "Liveforbindelsen til auktioner kunne ikke startes. Genindlæs siden for at prøve igen.";
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        isDisposed = true;

        AuctionRealtimeService.AuctionCreated -= OnAuctionCreatedAsync;
        AuctionRealtimeService.AuctionUpdated -= OnAuctionUpdatedAsync;
        AuctionRealtimeService.AuctionDeleted -= OnAuctionDeletedAsync;
    }

    /// <summary>
    /// Reloads the dashboard whenever an auction changes anywhere, so the list never shows stale or
    /// duplicated rows.
    /// </summary>
    /// <returns>A task that completes once the list has been reloaded.</returns>
    private Task OnAuctionCreatedAsync(CreateAuctionNotification notification) =>
        RefreshAuctionsAsync();

    /// <inheritdoc cref="OnAuctionCreatedAsync"/>
    private Task OnAuctionUpdatedAsync(UpdateAuctionNotification notification) =>
        RefreshAuctionsAsync();

    /// <inheritdoc cref="OnAuctionCreatedAsync"/>
    private Task OnAuctionDeletedAsync(DeleteAuctionNotification notification) =>
        RefreshAuctionsAsync();

    private Task RefreshAuctionsAsync()
    {
        if (isDisposed)
        {
            return Task.CompletedTask;
        }

        return InvokeAsync(async () =>
        {
            await LoadAuctionsAsync();
            StateHasChanged();
        });
    }

    // --- Opret og redigér -------------------------------------------------

    /// <summary>
    /// Creates or updates the auction described by the form and reloads the dashboard.
    /// </summary>
    private async Task SubmitAsync()
    {
        if (isSubmitting)
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

        if (startsAt is null || endsAt is null)
        {
            statusMessage = "Udfyld startdato, starttid og sluttid, før auktionen gemmes.";
            submissionSucceeded = false;
            return;
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
                    StartsAt = startsAt,
                    EndsAt = endsAt,
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
                    StartsAt = startsAt,
                    EndsAt = endsAt,
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

            editingAuctionId = detail.AuctionId;

            model = new CreateAuctionFormModel
            {
                Name = detail.Name,
                StartDate = detail.StartsAt.Date,
                StartTime = detail.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                EndTime = detail.EndsAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                EmployeeId = detail.EmployeeId,
                RequireFutureStart = detail.StartsAt > DateTime.Now
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
        isLoadingAuctions = true;
        auctionListError = null;

        try
        {
            auctions = await AuctionService.GetAllAsync();
            auctionPage = Math.Clamp(auctionPage, 1, TotalAuctionPages);
        }
        catch (AuctionApiException exception)
        {
            auctionListError = exception.Message;
        }
        catch (HttpRequestException)
        {
            auctionListError = "Listen over auktioner kunne ikke hentes. Prøv igen om lidt.";
        }
        catch (TaskCanceledException)
        {
            auctionListError = "Anmodningen om auktionslisten tog for lang tid. Prøv igen.";
        }
        finally
        {
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
        pendingDeleteId = auctionId;
        statusMessage = null;
    }

    private void CancelDelete() => pendingDeleteId = null;

    /// <summary>
    /// Deletes the auction awaiting confirmation and reloads the dashboard.
    /// </summary>
    private async Task ConfirmDeleteAsync()
    {
        if (pendingDeleteId is not { } auctionId || isDeleting || isSubmitting)
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
            employees = await EmployeeService.GetAllAsync();
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

    private static string DeriveStatus(DateTime startsAt, DateTime endsAt)
    {
        DateTime now = DateTime.Now;

        if (now < startsAt)
        {
            return StatusUpcoming;
        }

        return now < endsAt ? StatusLive : StatusEnded;
    }

    private static string StatusCssClass(string status) => status switch
    {
        StatusUpcoming => "badge-upcoming",
        StatusLive => "badge-live",
        _ => "badge-ended"
    };
}
