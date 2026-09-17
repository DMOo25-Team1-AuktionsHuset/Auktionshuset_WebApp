using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;
using Auktionshuset.Models;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net;


namespace Auktionshuset.Components.Pages;

public partial class Warehouse : IAsyncDisposable
{
    [Inject] private IConfiguration Configuration { get; set; } = default!;
    private HubConnection? hubConnection;

    [Inject] private LotService LotService { get; set; } = default!;

    private LotFormModel model = new();
    private EditContext editContext = default!;
    private bool isSubmitting;
    private bool submissionSucceeded;
    private string? statusMessage;
    private IReadOnlyList<LotListItemResponse> lots = [];
    private bool isLoadingLots;
    private string? lotListError;
    private Guid? editingLotId;

    private static readonly Guid DefaultAuctionHouseId = Guid.Parse("8cc2c7dc-6244-41e7-805f-a90f9279c540");
    private bool isDeleting;

    /// <summary>
    /// Resets the form and loads the current lot list.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        ResetForm();
        await LoadLotsAsync();
    }

    /// <summary>
    /// Creates a lot from the current form values and reloads the list.
    /// Returns immediately when a submit is already in progress.
    /// </summary>
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
                AuctionHouseId = DefaultAuctionHouseId
            };

            var response = await LotService.CreateAsync(request);
            var createdLotId = response.LotId;
            ResetForm();
            submissionSucceeded = true;
            statusMessage = $"Genstanden blev tilføjet.";
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

    /// <summary>
    /// Copies the selected lot into the form and switches the page into edit mode.
    /// </summary>
    /// <param name="lot">The lot whose values are loaded into the form.</param>
    private void BeginEdit(LotListItemResponse lot) {
        editingLotId = lot.LotId;

        model = new LotFormModel {
            Name = lot.Name,
            Category = lot.Category,
            Quantity = lot.Quantity,
            EstimatedValue = lot.EstimatedValue,
            Description = lot.Description,
            Tags = string.Join(", ", lot.Tags),
            AuctionHouseId = DefaultAuctionHouseId.ToString()
        };

        editContext = new EditContext(model);
        statusMessage = null;
        submissionSucceeded = false;
    }

    /// <summary>
    /// Saves the lot currently being edited and reloads the list.
    /// </summary>
    private async Task UpdateAsync() {
        var lotId = editingLotId;

        if (lotId == null || isSubmitting) {
            return;
        }

        if (!editContext.Validate()) {
            return;
        }

        isSubmitting = true;
        submissionSucceeded = false;
        statusMessage = null;

        try {
            var request = new UpdateLotRequest {
                Name = model.Name.Trim(),
                Category = model.Category.Trim(),
                Quantity = model.Quantity,
                EstimatedValue = model.EstimatedValue,
                Description = model.Description.Trim(),
                Tags = model.GetTags(),
                AuctionHouseId = Guid.Parse(model.AuctionHouseId)
            };

            var response = await LotService.UpdateAsync(lotId.Value, request);

            editingLotId = null;
            ResetForm();

            submissionSucceeded = true;
            statusMessage = $"Genstanden blev opdateret.";

            await LoadLotsAsync();
        } catch (LotApiException exception) {
            if(exception.StatusCode == HttpStatusCode.NotFound) {
                editingLotId = null;
                ResetForm();
            }

            statusMessage = exception.Message;
        } catch (HttpRequestException) {
            statusMessage = "Der kunne ikke oprettes forbindelse til serveren. Prøv igen om lidt.";
        } catch (TaskCanceledException) {
            statusMessage = "Anmodningen tog for lang tid. Prøv igen.";
        }
        finally {
            isSubmitting = false;
        }
    }

    /// <summary>
    /// Leaves edit mode and resets the form.
    /// </summary>
    private void CancelEdit() {
        editingLotId = null;
        statusMessage = null;
        submissionSucceeded = false;

        ResetForm();
    }

    /// <summary>
    /// Deletes the given lot and reloads the list.
    /// </summary>
    /// <param name="lotId">The identifier of the lot to delete.</param>
    public async Task DeleteAsync(Guid lotId)
    {

        if (isDeleting || isSubmitting)
        {
            return;
        }

        isDeleting = true;
        submissionSucceeded = false;
        statusMessage = null;

        try
        {
            await LotService.DeleteAsync(lotId, cancellationToken: default);
            submissionSucceeded = true;
            statusMessage = "Genstanden blev slettet.";
            await LoadLotsAsync();
            ResetForm();
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
            statusMessage = "Sletningen tog for lang tid. Prøv igen.";
        }
        finally
        {
            isDeleting = false;
        }
    }

    /// <summary>
    /// Fetches the lot list and stores any failure in the list error state for display.
    /// </summary>
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

    /// <summary>
    /// Replaces the form with a fresh model bound to the default auction house.
    /// </summary>
    private void ResetForm()
    {
        model = new LotFormModel {
            AuctionHouseId = DefaultAuctionHouseId.ToString()
        };
        editContext = new EditContext(model);
    }

    /// <summary>
    /// Disposes the SignalR connection used for live lot updates.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }

    /// <summary>
    /// Reloads the lot list on the component's UI thread and re-renders it.
    /// </summary>
    private Task RefreshLotsAsync()
    {
        return InvokeAsync(async () =>
        {
            await LoadLotsAsync();
            StateHasChanged();
        });
    }

    /// <summary>
    /// Starts the SignalR connection and subscribes to lot notifications after the first render.
    /// </summary>
    /// <param name="firstRender"><see langword="true"/> only for the component's first render.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the required configuration value <c>Api:BaseUrl</c> is missing.
    /// </exception>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        var apiBaseUrl = Configuration["Api:BaseUrl"]
                         ?? throw new InvalidOperationException(
                             "Configuration Value 'Api:BaseUrl' is required.");

        hubConnection = new HubConnectionBuilder()
            .WithUrl($"{apiBaseUrl.TrimEnd('/')}/hubs/lot")
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<CreateLotNotification>(
            nameof(ILotClient.LotCreatedAsync),
            notification => RefreshLotsAsync());

        hubConnection.On<UpdateLotNotification>(
            nameof(ILotClient.LotUpdatedAsync),
            _ => RefreshLotsAsync());

        hubConnection.Reconnected +=
            connectionId => RefreshLotsAsync();


        try
        {
            await hubConnection.StartAsync();

            // Hent igen efter tilslutning, så listen er ajour.
            await RefreshLotsAsync();
        }
        catch (Exception)
        {
            lotListError =
                "Liveforbindelsen kunne ikke startes. Genindlæs siden for at prøve igen.";

            StateHasChanged();
        }
    }
}