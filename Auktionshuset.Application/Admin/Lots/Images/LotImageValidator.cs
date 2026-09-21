namespace Auktionshuset.Application.Admin.Lots.Images;

/// <summary>
/// Validates uploaded lot images. Only the leading bytes of the file are trusted, never the file
/// name or the content type reported by the client.
/// </summary>
public static class LotImageValidator
{
    /// <summary>
    /// The largest image the API accepts: 5 MB.
    /// </summary>
    public const long MaxSizeInBytes = 5 * 1024 * 1024;

    /// <summary>
    /// The number of bytes that are enough to identify every supported image type.
    /// </summary>
    public const int HeaderLength = 12;

    /// <summary>
    /// Determines the file extension to store the image under by inspecting its magic bytes.
    /// </summary>
    /// <param name="header">The leading bytes of the uploaded file.</param>
    /// <param name="extension">The extension including the leading dot when the type is supported; otherwise an empty string.</param>
    /// <returns><see langword="true"/> when the bytes identify a supported image type; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetExtension(ReadOnlySpan<byte> header, out string extension)
    {
        extension = string.Empty;

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            extension = ".jpg";
            return true;
        }

        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (header.Length >= pngSignature.Length && header[..pngSignature.Length].SequenceEqual(pngSignature))
        {
            extension = ".png";
            return true;
        }

        // WebP is a RIFF container whose payload type is "WEBP".
        ReadOnlySpan<byte> riff = [0x52, 0x49, 0x46, 0x46];
        ReadOnlySpan<byte> webp = [0x57, 0x45, 0x42, 0x50];
        if (header.Length >= HeaderLength
            && header[..riff.Length].SequenceEqual(riff)
            && header[8..12].SequenceEqual(webp))
        {
            extension = ".webp";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks that every byte of the uploaded file is an accepted image.
    /// </summary>
    /// <param name="content">A readable, seekable stream holding the upload.</param>
    /// <param name="sizeInBytes">The length of the upload in bytes.</param>
    /// <param name="extension">The extension derived from the image header when the file is accepted.</param>
    /// <returns><see langword="true"/> when the upload is an accepted image; otherwise, <see langword="false"/>.</returns>
    public static bool TryValidate(Stream content, long sizeInBytes, out string extension)
    {
        extension = string.Empty;

        if (sizeInBytes <= 0 || sizeInBytes > MaxSizeInBytes || !content.CanRead || !content.CanSeek)
        {
            return false;
        }

        var header = new byte[HeaderLength];
        content.Position = 0;
        var read = content.Read(header, 0, header.Length);
        content.Position = 0;

        return TryGetExtension(header.AsSpan(0, read), out extension);
    }
}
