using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Models;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Auktionshuset.Components.Pages;

public partial class Lager
{
    [Inject]
    private LotService LotService { get; set; } = default!;

    private CreateLotFormModel model = new();
    private EditContext editContext = default!;
    private bool isSubmitting;
    private bool submissionSucceeded;
    private string? statusMessage;
    private IReadOnlyList<LotListItemResponse> lots = [];
    private bool isLoadingLots;
    private string? lotListError;

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
            var request = new CreateLotRequest
            {
                Name = model.Name.Trim(),
                Category = model.Category.Trim(),
                Quantity = model.Quantity,
                EstimatedValue = model.EstimatedValue,
                Description = model.Description.Trim(),
                Tags = model.GetTags(),
                AuctionHouseId = Guid.Parse(model.AuctionHouseId)
            };

            var response = await LotService.CreateAsync(request);
            var createdLotId = response.LotId;
            ResetForm();
            submissionSucceeded = true;
            statusMessage = $"Lottet blev tilføjet. Lot-id: {createdLotId}";
            await LoadLotsAsync();
        }
        catch (LotApiException exception)
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

    private void ResetForm()
    {
        model = new CreateLotFormModel();
        editContext = new EditContext(model);
    }
}
