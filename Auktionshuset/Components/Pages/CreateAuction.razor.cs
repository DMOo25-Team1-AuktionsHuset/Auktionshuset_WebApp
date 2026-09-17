using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Models;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Auktionshuset.Components.Pages;

public partial class CreateAuction : IDisposable
{
    private const int MaxLiveAuctions = 5;

    [Inject]
    private LotService LotService { get; set; } = default!;

    [Inject]
    private AuctionService AuctionService { get; set; } = default!;

    [Inject]
    private AuctionRealtimeService AuctionRealtimeService { get; set; } = default!;

    private CreateAuctionFormModel model = new();
    private EditContext editContext = default!;
    private readonly HashSet<Guid> selectedLotIds = [];
    private readonly List<CreateAuctionNotification> liveAuctions = [];
    private IReadOnlyList<LotListItemResponse> lots = [];
    private bool isLoadingLots;
    private string? lotListError;
    private string? realtimeError;
    private bool isSubmitting;
    private bool submissionSucceeded;
    private string? statusMessage;
    private bool isDisposed;

    private IReadOnlyList<LotListItemResponse> SelectedLots =>
        lots.Where(lot => selectedLotIds.Contains(lot.LotId)).ToArray();

    protected override async Task OnInitializedAsync()
    {
        ResetForm();
        await LoadLotsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        AuctionRealtimeService.AuctionCreated += OnAuctionCreatedAsync;

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
    }

    private Task OnAuctionCreatedAsync(CreateAuctionNotification notification)
    {
        if (isDisposed)
        {
            return Task.CompletedTask;
        }

        return InvokeAsync(() =>
        {
            liveAuctions.Insert(0, notification);

            if (liveAuctions.Count > MaxLiveAuctions)
            {
                liveAuctions.RemoveAt(liveAuctions.Count - 1);
            }

            StateHasChanged();
        });
    }

    private async Task SubmitAsync()
    {
        if (isSubmitting)
        {
            return;
        }

        isSubmitting = true;
        submissionSucceeded = false;
        statusMessage = null;

        try
        {
            var request = new CreateAuctionRequest
            {
                StartsAt = model.GetStartsAt(),
                LotIds = selectedLotIds.ToArray()
            };

            var response = await AuctionService.CreateAsync(request);

            ResetForm();
            submissionSucceeded = true;

            var lotsText = response.LotCount == 0
                ? "uden genstande"
                : $"med {response.LotCount} genstand{(response.LotCount == 1 ? string.Empty : "e")}";

            statusMessage = $"Auktionen blev oprettet {lotsText}.";
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

    private void ToggleLot(Guid lotId, ChangeEventArgs changeEventArgs)
    {
        if (changeEventArgs.Value is true)
        {
            selectedLotIds.Add(lotId);
        }
        else
        {
            selectedLotIds.Remove(lotId);
        }
    }

    private void ClearSelection() => selectedLotIds.Clear();

    private void ResetForm()
    {
        model = new CreateAuctionFormModel();
        editContext = new EditContext(model);
        selectedLotIds.Clear();
    }
}
