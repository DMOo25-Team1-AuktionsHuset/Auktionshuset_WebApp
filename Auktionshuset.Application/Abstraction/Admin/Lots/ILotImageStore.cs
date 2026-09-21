namespace Auktionshuset.Application.Abstraction.Admin.Lots;

/// <summary>
/// Stores and removes the image files attached to lots.
/// </summary>
public interface ILotImageStore
{
    /// <summary>
    /// Saves an image and returns the generated file name it is stored under.
    /// </summary>
    /// <param name="content">The image bytes to store.</param>
    /// <param name="extension">The file extension, including the leading dot, derived from the verified image type.</param>
    /// <returns>The generated, path-safe file name.</returns>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a stored image.
    /// </summary>
    /// <param name="fileName">The file name previously returned by <see cref="SaveAsync"/>.</param>
    /// <returns><see langword="true"/> when a file was removed; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken);
}
