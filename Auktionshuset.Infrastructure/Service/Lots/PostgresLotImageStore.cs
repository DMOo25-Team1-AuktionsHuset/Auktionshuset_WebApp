using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.Images;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Service.Lots;

public sealed class PostgresLotImageStore(AHDBContext dbContext) : ILotImageStore
{
    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        string contentType = extension.ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new ArgumentException("Unsupported image type.", nameof(extension))
        };

        using var buffer = new MemoryStream();
        if (content.CanSeek) content.Position = 0;
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0 || buffer.Length > LotImageValidator.MaxSizeInBytes)
        {
            throw new InvalidDataException("The image size is invalid.");
        }
        byte[] bytes = buffer.ToArray();
        if (!LotImageValidator.TryGetExtension(bytes.AsSpan(0, Math.Min(bytes.Length, LotImageValidator.HeaderLength)), out string detectedExtension)
            || detectedExtension != extension.ToLowerInvariant())
        {
            throw new InvalidDataException("The image content does not match its type.");
        }

        string fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        dbContext.StoredLotImages.Add(new StoredLotImage
        {
            FileName = fileName,
            Content = bytes,
            ContentType = contentType
        });

        return fileName;
    }

    public async Task<LotImageContent?> GetAsync(string fileName, CancellationToken cancellationToken)
    {
        if (!IsSafeFileName(fileName)) return null;

        return await dbContext.StoredLotImages
            .AsNoTracking()
            .Where(image => image.FileName == fileName)
            .Select(image => new LotImageContent(image.Content, image.ContentType))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSafeFileName(fileName)) return false;

        StoredLotImage? image = await dbContext.StoredLotImages.FindAsync([fileName], cancellationToken);

        if (image == null) return false;

        dbContext.StoredLotImages.Remove(image);

        return true;
    }

    private static bool IsSafeFileName(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        return fileName.Length <= 128
            && (extension is ".jpg" or ".png" or ".webp")
            && stem.Length == 32
            && stem.All(Uri.IsHexDigit);
    }
}
