namespace Auktionshuset.Application.Admin.Lots.Images;

/// <summary>
/// An uploaded image together with the metadata reported by the client.
/// </summary>
/// <param name="LotId">The identifier of the lot the image belongs to.</param>
/// <param name="FileName">The file name reported by the client. Only used for diagnostics, never for storage.</param>
/// <param name="ContentType">The content type reported by the client.</param>
/// <param name="Length">The number of bytes in <paramref name="Content"/>.</param>
/// <param name="Content">The uploaded bytes.</param>
public sealed record UploadLotImageCommand(
    Guid LotId,
    string? FileName,
    string? ContentType,
    long Length,
    Stream Content);

/// <summary>
/// The outcome of an image upload or removal.
/// </summary>
public sealed record LotImageResult
{
    public Guid LotId { get; private init; }
    public string? ImageFileName { get; private init; }
    public IReadOnlyCollection<string> Errors { get; private init; } = [];

    /// <summary>
    /// Gets a value indicating that the lot did not exist. This is not a validation failure.
    /// </summary>
    public bool NotFound { get; private init; }

    public bool Succeeded => Errors.Count == 0 && !NotFound;

    public static LotImageResult Saved(Guid lotId, string? imageFileName) =>
        new() { LotId = lotId, ImageFileName = imageFileName };

    public static LotImageResult Invalid(IReadOnlyCollection<string> errors) =>
        new() { Errors = errors };

    public static LotImageResult Missing() =>
        new() { NotFound = true };
}
