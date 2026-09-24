using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Contracts.Dto.Admin.Lot.DeleteLot;
using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;
using Auktionshuset.Models;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.SignalR.Client;
using System.Globalization;

namespace Auktionshuset.Components.Pages;

public partial class Warehouse : IAsyncDisposable
{
    /// <summary>
    /// The largest image the form accepts from the browser. The API enforces the same limit.
    /// </summary>
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private static readonly int[] PageSizeOptions = [10, 25, 50];

    private static readonly CultureInfo DanishCulture = CultureInfo.GetCultureInfo("da-DK");

    private static readonly Guid DefaultAuctionHouseId = Guid.Parse("8cc2c7dc-6244-41e7-805f-a90f9279c540");

    private readonly HashSet<Guid> expandedLotIds = [];

    [Inject] private IConfiguration Configuration { get; set; } = default!;

    [Inject] private LotService LotService { get; set; } = default!;

    private HubConnection? hubConnection;

    private LotFormModel model = new();
    private EditContext editContext = default!;

    private IReadOnlyList<LotListItemResponse> lots = [];
    private bool isLoadingLots;
    private string? lotListError;
    private string? realtimeError;

    private string searchTerm = string.Empty;
    private int pageSize = 10;
    private int page = 1;

    private Guid? editingLotId;
    private Guid? pendingDeleteId;

    private bool isSubmitting;
    private bool isDeleting;
    private bool submissionSucceeded;
    private string? statusMessage;

    private byte[]? pendingImage;
    private string? pendingImageFileName;
    private string? pendingImageContentType;
    private string? pendingImagePreviewUrl;
    private string? existingImageUrl;
    private bool imageRemoved;
    private string? imageError;

    /// <summary>
    /// Gets the lots that match the current search, before paging.
    /// </summary>
    private IReadOnlyList<LotListItemResponse> FilteredLots
    {
        get
        {
            string term = searchTerm.Trim();

            if (term.Length == 0)
            {
                return lots;
            }

            return lots
                .Where(lot =>
                    Contains(lot.Name, term)
                    || Contains(lot.Category, term)
                    || Contains(lot.Description, term)
                    || lot.Tags.Any(tag => Contains(tag, term)))
                .ToArray();
        }
    }

    /// <summary>
    /// Gets the lots on the current page.
    /// </summary>
    private IReadOnlyList<LotListItemResponse> PagedLots =>
        FilteredLots.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(FilteredLots.Count / (double)pageSize));

    /// <summary>
    /// Gets the image shown in the picker: the pending upload, the stored image, or nothing.
    /// </summary>
    private string? PreviewImageUrl =>
        pendingImagePreviewUrl ?? (imageRemoved ? null : existingImageUrl);

    protected override async Task OnInitializedAsync()
    {
        ResetForm();
        await LoadLotsAsync();
    }

    /// <summary>
    /// Creates or updates the genstand described by the form, saves its image and reloads the list.
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
        imageError = null;

        try
        {
            string successMessage = await SaveLotAsync();

            await LoadLotsAsync();
            ResetForm();

            submissionSucceeded = true;
            statusMessage = successMessage;
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
    /// Sends the form values to the API and stores the pending image change for the saved genstand.
    /// </summary>
    /// <returns>The message describing what happened.</returns>
    private async Task<string> SaveLotAsync()
    {
        if (editingLotId is { } lotId)
        {
            await LotService.UpdateAsync(lotId, new UpdateLotRequest
            {
                Name = model.Name.Trim(),
                Category = model.Category.Trim(),
                Quantity = model.Quantity,
                EstimatedValue = model.EstimatedValue,
                Description = model.Description.Trim(),
                Tags = model.GetTags(),
                AuctionHouseId = Guid.Parse(model.AuctionHouseId)
            });

            await PersistImageAsync(lotId);

            return "Genstanden blev opdateret.";
        }

        CreateLotResponse response = await LotService.CreateAsync(new CreateLotRequest
        {
            Name = model.Name.Trim(),
            Category = model.Category.Trim(),
            Quantity = model.Quantity,
            EstimatedValue = model.EstimatedValue,
            Description = model.Description.Trim(),
            Tags = model.GetTags(),
            AuctionHouseId = DefaultAuctionHouseId
        });

        await PersistImageAsync(response.LotId);

        return "Genstanden blev oprettet.";
    }

    /// <summary>
    /// Uploads a newly chosen image or removes the stored one, depending on what the form holds.
    /// </summary>
    /// <param name="lotId">The identifier of the saved genstand.</param>
    private async Task PersistImageAsync(Guid lotId)
    {
        if (pendingImage is not null)
        {
            using var stream = new MemoryStream(pendingImage, writable: false);

            await LotService.UploadImageAsync(
                lotId,
                stream,
                pendingImageFileName ?? "billede",
                pendingImageContentType ?? "application/octet-stream");

            return;
        }

        if (imageRemoved)
        {
            await LotService.RemoveImageAsync(lotId);
        }
    }

    /// <summary>
    /// Copies the selected genstand into the form and switches the page into edit mode.
    /// </summary>
    /// <param name="lot">The genstand whose values are loaded into the form.</param>
    private void BeginEdit(LotListItemResponse lot)
    {
        editingLotId = lot.LotId;

        model = new LotFormModel
        {
            Name = lot.Name,
            Category = lot.Category,
            Quantity = lot.Quantity,
            EstimatedValue = lot.EstimatedValue,
            Description = lot.Description,
            Tags = string.Join(", ", lot.Tags),
            AuctionHouseId = lot.AuctionHouseId.ToString()
        };

        editContext = new EditContext(model);

        ClearImageState();
        existingImageUrl = LotService.ResolveImageUrl(lot.ImageUrl);

        pendingDeleteId = null;
        statusMessage = null;
        submissionSucceeded = false;
        imageError = null;
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

    private void RequestDelete(Guid lotId)
    {
        pendingDeleteId = lotId;
        statusMessage = null;
    }

    private void CancelDelete() => pendingDeleteId = null;

    /// <summary>
    /// Deletes the genstand awaiting confirmation and reloads the list.
    /// </summary>
    private async Task ConfirmDeleteAsync()
    {
        if (pendingDeleteId is not { } lotId || isDeleting || isSubmitting)
        {
            return;
        }

        isDeleting = true;
        submissionSucceeded = false;
        statusMessage = null;

        try
        {
            await LotService.DeleteAsync(lotId);

            if (editingLotId == lotId)
            {
                ResetForm();
            }
            else
            {
                pendingDeleteId = null;
            }

            await LoadLotsAsync();

            submissionSucceeded = true;
            statusMessage = "Genstanden blev slettet.";
        }
        catch (LotApiException exception)
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

    private void ToggleExpanded(Guid lotId)
    {
        if (!expandedLotIds.Remove(lotId))
        {
            expandedLotIds.Add(lotId);
        }
    }

    private void OnSearchChanged() => page = 1;

    private void OnPageSizeChanged() => page = 1;

    private void GoToPreviousPage()
    {
        if (page > 1)
        {
            page--;
        }
    }

    private void GoToNextPage()
    {
        if (page < TotalPages)
        {
            page++;
        }
    }

    /// <summary>
    /// Validates and stages the image the user picked, so it can be uploaded on save.
    /// </summary>
    /// <param name="args">The file supplied by the browser.</param>
    private async Task OnImageSelectedAsync(InputFileChangeEventArgs args)
    {
        imageError = null;

        IBrowserFile file = args.File;

        if (file.Size <= 0)
        {
            imageError = "Vælg en billedfil, der skal vedhæftes genstanden.";
            return;
        }

        if (file.Size > MaxImageBytes)
        {
            imageError = "Billedet må højst være 5 MB.";
            return;
        }

        string extension = Path.GetExtension(file.Name);

        if (!AllowedImageExtensions.Contains(extension))
        {
            imageError = "Billedet skal være i formatet JPEG, PNG eller WebP.";
            return;
        }

        try
        {
            await using Stream stream = file.OpenReadStream(MaxImageBytes);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);

            pendingImage = buffer.ToArray();
            pendingImageFileName = Path.GetFileName(file.Name);
            pendingImageContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? ContentTypeFor(extension)
                : file.ContentType;
            pendingImagePreviewUrl =
                $"data:{pendingImageContentType};base64,{Convert.ToBase64String(pendingImage)}";
            imageRemoved = false;
        }
        catch (IOException)
        {
            imageError = "Billedet kunne ikke læses. Prøv at vælge filen igen.";
        }
    }

    /// <summary>
    /// Drops a staged image, or marks the stored image for removal.
    /// </summary>
    private void RemoveImage()
    {
        bool hadStoredImage = existingImageUrl is not null;

        ClearPendingImage();
        imageError = null;
        imageRemoved = hadStoredImage;
    }

    /// <summary>
    /// Fetches the lot list. The search term, page and page size are left untouched so a live
    /// update cannot reset what the user is looking at.
    /// </summary>
    private async Task LoadLotsAsync()
    {
        isLoadingLots = true;
        lotListError = null;

        try
        {
            lots = await LotService.GetAllAsync();
            page = Math.Clamp(page, 1, TotalPages);
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

    /// <summary>
    /// Clears the form back to an empty creation form.
    /// </summary>
    private void ResetForm()
    {
        editingLotId = null;
        pendingDeleteId = null;
        imageError = null;

        model = new LotFormModel
        {
            AuctionHouseId = DefaultAuctionHouseId.ToString()
        };

        editContext = new EditContext(model);

        ClearImageState();
    }

    private void ClearImageState()
    {
        ClearPendingImage();
        existingImageUrl = null;
        imageRemoved = false;
    }

    private void ClearPendingImage()
    {
        pendingImage = null;
        pendingImageFileName = null;
        pendingImageContentType = null;
        pendingImagePreviewUrl = null;
    }

    private static bool Contains(string value, string term) =>
        value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };

    /// <summary>
    /// Reloads the list on the component's UI thread and re-renders it.
    /// </summary>
    private Task RefreshLotsAsync() =>
        InvokeAsync(async () =>
        {
            await LoadLotsAsync();
            StateHasChanged();
        });

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

        string apiBaseUrl = Configuration["Api:BaseUrl"]
                         ?? throw new InvalidOperationException(
                             "Configuration Value 'Api:BaseUrl' is required.");

        hubConnection = new HubConnectionBuilder()
            .WithUrl($"{apiBaseUrl.TrimEnd('/')}/hubs/lot")
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<CreateLotNotification>(
            nameof(ILotClient.LotCreatedAsync),
            _ => RefreshLotsAsync());

        hubConnection.On<UpdateLotNotification>(
            nameof(ILotClient.LotUpdatedAsync),
            _ => RefreshLotsAsync());

        hubConnection.On<DeleteLotNotification>(
            nameof(ILotClient.LotDeletedAsync),
            _ => RefreshLotsAsync());

        hubConnection.Reconnected +=
            _ => RefreshLotsAsync();

        try
        {
            await hubConnection.StartAsync();

            // Hent igen efter tilslutning, så listen er ajour.
            await RefreshLotsAsync();
        }
        catch (Exception)
        {
            realtimeError =
                "Liveforbindelsen kunne ikke startes. Genindlæs siden for at prøve igen.";

            StateHasChanged();
        }
    }

    /// <summary>
    /// Disposes the SignalR connection used for live lot updates.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
            hubConnection = null;
        }
    }
}
