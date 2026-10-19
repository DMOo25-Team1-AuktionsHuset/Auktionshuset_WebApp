using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service.Lots;
using Auktionshuset.Application.Admin.Lots.Images;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Extensions;

public static class DBStartExtensionHelper
{
    /// <summary>
    /// Migrates the database, seeds initial data, and imports any legacy local lot images.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task MigrateAndSeedDatabaseAsync(this WebApplication app)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        AHDBContext context = scope.ServiceProvider.GetRequiredService<AHDBContext>();

        await context.Database.MigrateAsync();
        await DBSeeder.SeedAsync(context);
        await ImportLocalLotImagesAsync(context, app.Logger);
    }

    // Import images from previous installations, retaining their names and URLs.
    private static async Task ImportLocalLotImagesAsync(AHDBContext context, ILogger logger)
    {
        string rootPath = LotImageStoreOptions.DefaultRootPath;
        if (!Directory.Exists(rootPath)) return;

        string[] fileNames = await context.Lot.AsNoTracking()
            .Where(lot => lot.ImageFileName != null)
            .Select(lot => lot.ImageFileName!)
            .Distinct()
            .ToArrayAsync();

        foreach (string fileName in fileNames)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            string stem = Path.GetFileNameWithoutExtension(fileName);
            if (!Guid.TryParseExact(stem, "N", out _)
                || extension is not (".jpg" or ".png" or ".webp")
                || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
                || await context.StoredLotImages.AnyAsync(image => image.FileName == fileName))
            {
                continue;
            }

            string path = Path.Combine(rootPath, fileName);
            if (!File.Exists(path)) continue;

            try
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length is <= 0 or > LotImageValidator.MaxSizeInBytes) continue;

                byte[] bytes = await File.ReadAllBytesAsync(path);
                if (!LotImageValidator.TryGetExtension(bytes.AsSpan(0, Math.Min(bytes.Length, LotImageValidator.HeaderLength)), out string detectedExtension)
                    || detectedExtension != extension)
                {
                    logger.LogWarning("Skipping legacy lot image {FileName}: content does not match its extension", fileName);
                    continue;
                }

                context.StoredLotImages.Add(new StoredLotImage
                {
                    FileName = fileName,
                    Content = bytes,
                    ContentType = extension switch
                    {
                        ".jpg" => "image/jpeg",
                        ".png" => "image/png",
                        _ => "image/webp"
                    }
                });
                await context.SaveChangesAsync();
            }
            catch (IOException exception)
            {
                logger.LogWarning(exception, "Could not import legacy lot image {FileName}", fileName);
            }
        }
    }
}
