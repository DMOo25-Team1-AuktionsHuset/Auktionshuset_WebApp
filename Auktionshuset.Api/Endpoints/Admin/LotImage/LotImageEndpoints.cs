using Auktionshuset.Application.Admin.Lots.Images;
using Auktionshuset.Contracts.Dto.Admin.Lot.Image;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Endpoints.Admin.LotImage;

/// <summary>
/// Maps the endpoints that attach and remove the image of a lot. Images are uploaded as
/// multipart/form-data on their own endpoint, so the JSON lot endpoints stay unchanged.
/// </summary>
public static class LotImageEndpoints
{
    private const string FileFieldName = "file";

    /// <summary>
    /// Maps the lot image endpoints onto the supplied route group.
    /// </summary>
    /// <param name="group">The route group that the endpoints are mapped onto.</param>
    /// <returns>The same route group so that further endpoints can be chained.</returns>
    public static RouteGroupBuilder MapLotImageEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{lotId:guid}/image", HandleUploadAsync)
            .WithName("UploadLotImage")
            .WithSummary("Attaches an image to a lot")
            .Produces<LotImageResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .DisableAntiforgery();

        group.MapDelete("/{lotId:guid}/image", HandleRemoveAsync)
            .WithName("RemoveLotImage")
            .WithSummary("Removes the image of a lot")
            .Produces<LotImageResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// Reads the uploaded file, hands it to the handler for validation and storage, and returns the
    /// new image URL.
    /// </summary>
    /// <param name="lotId">The identifier of the lot the image belongs to.</param>
    /// <param name="request">The multipart request containing the image.</param>
    /// <param name="handler">The handler that validates and stores the image.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The stored image URL, a validation problem, or 404 when the lot does not exist.</returns>
    public static async Task<Results<Ok<LotImageResponse>, NotFound, ValidationProblem>> HandleUploadAsync(
        Guid lotId,
        HttpRequest request,
        UploadLotImageHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Invalid("Vedhæft billedet som en fil.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.Count == 1
            ? form.Files[0]
            : form.Files.FirstOrDefault(candidate => candidate.Name == FileFieldName);

        if (file is null || file.Length == 0)
        {
            return Invalid("Vælg en billedfil, der skal vedhæftes genstanden.");
        }

        if (file.Length > LotImageValidator.MaxSizeInBytes)
        {
            return Invalid("Billedet må højst være 5 MB.");
        }

        // Copy the upload into memory so the validator and the store always receive a seekable
        // stream, and so the request's temporary file can be released before the response is written.
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var command = new UploadLotImageCommand(
            LotId: lotId,
            FileName: file.FileName,
            ContentType: file.ContentType,
            Length: buffer.Length,
            Content: buffer);

        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.NotFound)
        {
            return TypedResults.NotFound();
        }

        if (!result.Succeeded)
        {
            return Invalid([.. result.Errors]);
        }

        return TypedResults.Ok(new LotImageResponse(result.LotId, LotImagePaths.ToUrl(result.ImageFileName)));
    }

    /// <summary>
    /// Removes the image of the lot identified by the route.
    /// </summary>
    /// <param name="lotId">The identifier of the lot whose image is removed.</param>
    /// <param name="handler">The handler that removes the image.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The lot without an image, or 404 when the lot does not exist.</returns>
    public static async Task<Results<Ok<LotImageResponse>, NotFound>> HandleRemoveAsync(
        Guid lotId,
        RemoveLotImageHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(lotId, cancellationToken);

        return result.NotFound
            ? TypedResults.NotFound()
            : TypedResults.Ok(new LotImageResponse(result.LotId, LotImagePaths.ToUrl(result.ImageFileName)));
    }

    private static ValidationProblem Invalid(params string[] messages) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [FileFieldName] = messages
        });
}
