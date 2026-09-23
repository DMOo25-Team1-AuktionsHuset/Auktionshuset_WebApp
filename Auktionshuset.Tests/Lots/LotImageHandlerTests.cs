using Xunit;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.Images;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;

namespace Auktionshuset.Tests;

public class LotImageHandlerTests
{
    /// <summary>
    /// Verifies that a valid upload is stored, referenced by the lot and announced to clients.
    /// </summary>
    [Fact]
    public async Task Upload_WithValidImage_StoresReferenceAndPublishesUpdate()
    {
        var lot = TestData.CreateLot("Vase");
        var repository = await TestData.CreateLotRepositoryAsync(lot);
        FakeLotImageStore store = new FakeLotImageStore { NextFileName = "abc123.png" };
        RecordingEventPublisher publisher = new RecordingEventPublisher();
        UploadLotImageHandler handler = new UploadLotImageHandler(repository, store, publisher);
        var bytes = LotImageValidatorTests.ImageBytes("png");

        var result = await handler.HandleAsync(
            CreateUploadCommand(lot.LotId, bytes, "min-vase.png", "image/png"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("abc123.png", result.ImageFileName);
        Assert.Equal(".png", store.SavedExtension);
        Assert.Equal(bytes.Length, store.SavedLength);

        var stored = await repository.GetByIdAsync(lot.LotId, CancellationToken.None);
        Assert.Equal("abc123.png", stored!.ImageFileName);

        var published = Assert.Single(publisher.Published.OfType<LotUpdatedIntegrationEvent>());
        Assert.Equal(lot.LotId, published.LotId);
        Assert.Equal("abc123.png", published.ImageFileName);
    }

    /// <summary>
    /// Verifies that replacing an image removes the previous file.
    /// </summary>
    [Fact]
    public async Task Upload_WhenImageAlreadyExists_RemovesPreviousFile()
    {
        var lot = TestData.CreateLot("Vase", imageFileName: "tidligere.jpg");
        var repository = await TestData.CreateLotRepositoryAsync(lot);
        FakeLotImageStore store = new FakeLotImageStore { NextFileName = "nyt.webp" };
        UploadLotImageHandler handler = new UploadLotImageHandler(repository, store, new RecordingEventPublisher());

        var result = await handler.HandleAsync(
            CreateUploadCommand(lot.LotId, LotImageValidatorTests.ImageBytes("webp"), "nyt.webp", "image/webp"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("tidligere.jpg", Assert.Single(store.Deleted));
        Assert.Equal("nyt.webp", (await repository.GetByIdAsync(lot.LotId, CancellationToken.None))!.ImageFileName);
    }

    /// <summary>
    /// Verifies that an upload for a genstand that does not exist reports a missing genstand.
    /// </summary>
    [Fact]
    public async Task Upload_WithUnknownLot_ReportsNotFound()
    {
        var repository = await TestData.CreateLotRepositoryAsync();
        FakeLotImageStore store = new FakeLotImageStore();
        RecordingEventPublisher publisher = new RecordingEventPublisher();
        UploadLotImageHandler handler = new UploadLotImageHandler(repository, store, publisher);

        var result = await handler.HandleAsync(
            CreateUploadCommand(Guid.NewGuid(), LotImageValidatorTests.ImageBytes("png"), "billede.png", "image/png"),
            CancellationToken.None);

        Assert.True(result.NotFound);
        Assert.False(result.Succeeded);
        Assert.Null(store.SavedExtension);
        Assert.Empty(publisher.Published);
    }

    /// <summary>
    /// Verifies that an empty upload is rejected with a Danish message.
    /// </summary>
    [Fact]
    public async Task Upload_WithEmptyFile_IsRejected()
    {
        var lot = TestData.CreateLot("Vase");
        var repository = await TestData.CreateLotRepositoryAsync(lot);
        FakeLotImageStore store = new FakeLotImageStore();
        UploadLotImageHandler handler = new UploadLotImageHandler(repository, store, new RecordingEventPublisher());

        var result = await handler.HandleAsync(
            CreateUploadCommand(lot.LotId, [], "tom.png", "image/png"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("Vælg en billedfil"));
        Assert.Null(store.SavedExtension);
    }

    /// <summary>
    /// Verifies that a file larger than the limit is rejected without being stored.
    /// </summary>
    [Fact]
    public async Task Upload_WithTooLargeFile_IsRejected()
    {
        var lot = TestData.CreateLot("Vase");
        var repository = await TestData.CreateLotRepositoryAsync(lot);
        FakeLotImageStore store = new FakeLotImageStore();
        UploadLotImageHandler handler = new UploadLotImageHandler(repository, store, new RecordingEventPublisher());

        var command = CreateUploadCommand(
            lot.LotId,
            LotImageValidatorTests.ImageBytes("png"),
            "stort.png",
            "image/png") with
        {
            Length = LotImageValidator.MaxSizeInBytes + 1
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("5 MB"));
        Assert.Null(store.SavedExtension);
    }

    /// <summary>
    /// Verifies that content that is not an image is rejected even though the client claims a type.
    /// </summary>
    [Fact]
    public async Task Upload_WithUnsupportedContent_IsRejected()
    {
        var lot = TestData.CreateLot("Vase");
        var repository = await TestData.CreateLotRepositoryAsync(lot);
        FakeLotImageStore store = new FakeLotImageStore();
        UploadLotImageHandler handler = new UploadLotImageHandler(repository, store, new RecordingEventPublisher());
        var bytes = "dette er ikke et billede"u8.ToArray();

        var result = await handler.HandleAsync(
            CreateUploadCommand(lot.LotId, bytes, "snyd.png", "image/png"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("JPEG, PNG eller WebP"));
        Assert.Null(store.SavedExtension);
        Assert.Null((await repository.GetByIdAsync(lot.LotId, CancellationToken.None))!.ImageFileName);
    }

    /// <summary>
    /// Verifies that removing an image clears the reference, deletes the file and announces the change.
    /// </summary>
    [Fact]
    public async Task Remove_WithStoredImage_ClearsReferenceAndDeletesFile()
    {
        var lot = TestData.CreateLot("Vase", imageFileName: "billede.jpg");
        var repository = await TestData.CreateLotRepositoryAsync(lot);
        FakeLotImageStore store = new FakeLotImageStore();
        RecordingEventPublisher publisher = new RecordingEventPublisher();
        RemoveLotImageHandler handler = new RemoveLotImageHandler(repository, store, publisher);

        var result = await handler.HandleAsync(lot.LotId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ImageFileName);
        Assert.Equal("billede.jpg", Assert.Single(store.Deleted));
        Assert.Null((await repository.GetByIdAsync(lot.LotId, CancellationToken.None))!.ImageFileName);

        var published = Assert.Single(publisher.Published.OfType<LotUpdatedIntegrationEvent>());
        Assert.Null(published.ImageFileName);
    }

    /// <summary>
    /// Verifies that removing an image from a genstand without one is not an error and deletes nothing.
    /// </summary>
    [Fact]
    public async Task Remove_WithoutStoredImage_SucceedsWithoutDeleting()
    {
        var lot = TestData.CreateLot("Vase");
        var repository = await TestData.CreateLotRepositoryAsync(lot);
        FakeLotImageStore store = new FakeLotImageStore();
        RecordingEventPublisher publisher = new RecordingEventPublisher();
        RemoveLotImageHandler handler = new RemoveLotImageHandler(repository, store, publisher);

        var result = await handler.HandleAsync(lot.LotId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ImageFileName);
        Assert.Empty(store.Deleted);
        Assert.Empty(publisher.Published);
    }

    /// <summary>
    /// Verifies that removing an image from a genstand that does not exist reports a missing genstand.
    /// </summary>
    [Fact]
    public async Task Remove_WithUnknownLot_ReportsNotFound()
    {
        var repository = await TestData.CreateLotRepositoryAsync();
        FakeLotImageStore store = new FakeLotImageStore();
        RemoveLotImageHandler handler = new RemoveLotImageHandler(repository, store, new RecordingEventPublisher());

        var result = await handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.NotFound);
        Assert.Empty(store.Deleted);
    }

    private static UploadLotImageCommand CreateUploadCommand(
        Guid lotId,
        byte[] content,
        string fileName,
        string contentType) => new(
            LotId: lotId,
            FileName: fileName,
            ContentType: contentType,
            Length: content.Length,
            Content: new MemoryStream(content));

    /// <summary>
    /// An image store that records what it was asked to do instead of touching the file system.
    /// </summary>
    private sealed class FakeLotImageStore : ILotImageStore
    {
        public string NextFileName { get; init; } = "genereret.png";
        public string? SavedExtension { get; private set; }
        public long SavedLength { get; private set; }
        public List<string> Deleted { get; } = [];

        public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
        {
            using MemoryStream buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);

            SavedExtension = extension;
            SavedLength = buffer.Length;

            return NextFileName;
        }

        public Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            Deleted.Add(fileName);
            return Task.FromResult(true);
        }
    }
}
