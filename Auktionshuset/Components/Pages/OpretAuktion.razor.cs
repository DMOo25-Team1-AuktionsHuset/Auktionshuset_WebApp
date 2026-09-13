using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Models;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Auktionshuset.Components.Pages;

public partial class OpretAuktion
{
    [Inject]
    private LotService LotService { get; set; } = default!;

    [Inject]
    private AuctionService AuctionService { get; set; } = default!;

    private CreateAuctionFormModel model = new();
    private EditContext editContext = default!;
    private readonly HashSet<Guid> selectedLotIds = [];
    private IReadOnlyList<LotListItemResponse> lots = [];
    private bool isLoadingLots;
    private string? lotListError;
    private bool isSubmitting;
    private bool submissionSucceeded;
    private string? statusMessage;

    private IReadOnlyList<LotListItemResponse> SelectedLots =>
        lots.Where(lot => selectedLotIds.Contains(lot.LotId)).ToArray();

    protected override async Task OnInitializedAsync()
    {
        ResetForm();
        await LoadLotsAsync();
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
                ? "uden lots"
                : $"med {response.LotCount} lot{(response.LotCount == 1 ? string.Empty : "s")}";

            statusMessage = $"Auktionen blev oprettet {lotsText}. Auktions-id: {response.AuctionId}";
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
            lotListError = "Listen over lots kunne ikke hentes. Prøv igen om lidt.";
        }
        catch (TaskCanceledException)
        {
            lotListError = "Anmodningen om lotlisten tog for lang tid. Prøv igen.";
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
