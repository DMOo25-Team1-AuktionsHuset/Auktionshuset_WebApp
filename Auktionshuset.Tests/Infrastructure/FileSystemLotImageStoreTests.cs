using Xunit;
using Auktionshuset.Infrastructure.Service.Lots;

namespace Auktionshuset.Tests;

public sealed class FileSystemLotImageStoreTests : IDisposable
{
    private readonly string rootPath =
        Path.Combine(Path.GetTempPath(), "auktionshuset-tests", Guid.NewGuid().ToString("N"));

    private FileSystemLotImageStore CreateStore() => new(new LotImageStoreOptions { RootPath = rootPath });

    /// <summary>
    /// Verifies that saving writes the bytes under a server-generated name with the verified extension.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithValidImage_WritesGeneratedFileName()
    {
        var store = CreateStore();
        var bytes = LotImageValidatorTests.ImageBytes("png");

        var fileName = await store.SaveAsync(new MemoryStream(bytes), ".png", CancellationToken.None);

        Assert.EndsWith(".png", fileName, StringComparison.Ordinal);
        Assert.DoesNotContain('/', fileName);
        Assert.DoesNotContain('\\', fileName);

        var written = await File.ReadAllBytesAsync(Path.Combine(rootPath, fileName));
        Assert.Equal(bytes, written);
    }

    /// <summary>
    /// Verifies that a saved file name never reuses the name supplied by a client.
    /// </summary>
    [Fact]
    public async Task SaveAsync_Twice_GeneratesDifferentFileNames()
    {
        var store = CreateStore();
        var bytes = LotImageValidatorTests.ImageBytes("jpeg");

        var first = await store.SaveAsync(new MemoryStream(bytes), ".jpg", CancellationToken.None);
        var second = await store.SaveAsync(new MemoryStream(bytes), ".jpg", CancellationToken.None);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// Verifies that an extension outside the supported image types is refused.
    /// </summary>
    [Theory]
    [InlineData(".exe")]
    [InlineData(".svg")]
    [InlineData("")]
    public async Task SaveAsync_WithUnsupportedExtension_Throws(string extension)
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(new MemoryStream([0x01]), extension, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that deleting removes a stored file.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WithStoredFile_RemovesIt()
    {
        var store = CreateStore();
        var fileName = await store.SaveAsync(new MemoryStream(LotImageValidatorTests.ImageBytes("webp")), ".webp", CancellationToken.None);

        var deleted = await store.DeleteAsync(fileName, CancellationToken.None);

        Assert.True(deleted);
        Assert.False(File.Exists(Path.Combine(rootPath, fileName)));
    }

    /// <summary>
    /// Verifies that a file name trying to leave the store folder is refused and touches no file.
    /// </summary>
    [Theory]
    [InlineData("../udenfor.png")]
    [InlineData("..\\udenfor.png")]
    [InlineData("undermappe/udenfor.png")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\billede.png")]
    public async Task DeleteAsync_WithPathTraversal_IsRefused(string fileName)
    {
        Directory.CreateDirectory(rootPath);
        var outside = Path.Combine(Path.GetDirectoryName(rootPath)!, "udenfor.png");
        await File.WriteAllBytesAsync(outside, [0x01, 0x02]);

        try
        {
            var deleted = await CreateStore().DeleteAsync(fileName, CancellationToken.None);

            Assert.False(deleted);
            Assert.True(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    /// <summary>
    /// Verifies that deleting a file that is not there reports false instead of throwing.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WithUnknownFile_ReturnsFalse()
    {
        var deleted = await CreateStore().DeleteAsync("findes-ikke.png", CancellationToken.None);

        Assert.False(deleted);
    }

    /// <summary>
    /// Verifies that deleting honours cancellation.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WhenCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateStore().DeleteAsync("billede.png", cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }

        var parent = Path.GetDirectoryName(rootPath);

        if (parent is not null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
        {
            Directory.Delete(parent);
        }
    }
}
