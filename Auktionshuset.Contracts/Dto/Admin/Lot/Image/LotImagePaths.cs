namespace Auktionshuset.Contracts.Dto.Admin.Lot.Image;

/// <summary>
/// Defines how lot images are addressed so the API and the clients agree on the convention.
/// </summary>
public static class LotImagePaths
{
    /// <summary>
    /// The request path that lot images are served from.
    /// </summary>
    public const string RequestPath = "/uploads/lots";

    /// <summary>
    /// Builds the relative image URL for a stored file name.
    /// </summary>
    /// <param name="fileName">The stored file name, or <see langword="null"/> when the lot has no image.</param>
    /// <returns>The relative URL, or <see langword="null"/> when no file name was supplied.</returns>
    public static string? ToUrl(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName)
            ? null
            : $"{RequestPath}/{fileName}";
}
