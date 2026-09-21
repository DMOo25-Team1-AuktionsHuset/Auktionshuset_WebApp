namespace Auktionshuset.Contracts.Dto.Admin.Lot.Image;

/// <summary>
/// The image reference of a lot. <see cref="ImageUrl"/> is relative to the API root and is
/// <see langword="null"/> when the lot has no image.
/// </summary>
public sealed record LotImageResponse(Guid LotId, string? ImageUrl);
