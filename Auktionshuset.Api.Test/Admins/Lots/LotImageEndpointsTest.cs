using System.Net.Http.Headers;
using Auktionshuset.Api.Endpoints.Admin.LotImage;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.Images;
using Auktionshuset.Contracts.Dto.Admin.Lot.Image;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Test.Admins.Lots;

public class LotImageEndpointsTest
{
    /// <summary>
    /// Verifies that a valid upload returns the stored image URL.
    /// </summary>
    [Fact]
    public async Task Upload_WithValidImage_ReturnsImageUrl()
    {
        var lot = CreateLot();
        var repository = await CreateRepositoryAsync(lot);
        var handler = new UploadLotImageHandler(
            repository,
            new FakeLotImageStore { NextFileName = "abc123.png" },
            new RecordingEventPublisher());

        var result = await LotImageEndpoints.HandleUploadAsync(
            lot.LotId,
            CreateMultipartRequest(PngBytes(), "min-genstand.png", "image/png"),
            handler,
            CancellationToken.None);

        var ok = Assert.IsType<Ok<LotImageResponse>>(result.Result);
        Assert.Equal(lot.LotId, ok.Value!.LotId);
        Assert.Equal("/uploads/lots/abc123.png", ok.Value.ImageUrl);
    }

    /// <summary>
    /// Verifies that an upload for a genstand that does not exist returns 404.
    /// </summary>
    [Fact]
    public async Task Upload_WithUnknownLot_ReturnsNotFound()
    {
        var repository = await CreateRepositoryAsync();
        var handler = new UploadLotImageHandler(repository, new FakeLotImageStore(), new RecordingEventPublisher());

        var result = await LotImageEndpoints.HandleUploadAsync(
            Guid.NewGuid(),
            CreateMultipartRequest(PngBytes(), "billede.png", "image/png"),
            handler,
            CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    /// <summary>
    /// Verifies that a multipart request without a file is rejected with a Danish message.
    /// </summary>
    [Fact]
    public async Task Upload_WithoutFile_ReturnsValidationProblem()
    {
        var lot = CreateLot();
        var repository = await CreateRepositoryAsync(lot);
        var handler = new UploadLotImageHandler(repository, new FakeLotImageStore(), new RecordingEventPublisher());

        var result = await LotImageEndpoints.HandleUploadAsync(
            lot.LotId,
            CreateMultipartRequest(content: null, fileName: null, contentType: null),
            handler,
            CancellationToken.None);

        AssertValidationMessage(result, "Vælg en billedfil");
    }

    /// <summary>
    /// Verifies that content that is not a multipart form is rejected with a Danish message.
    /// </summary>
    [Fact]
    public async Task Upload_WithoutMultipartContent_ReturnsValidationProblem()
    {
        var lot = CreateLot();
        var repository = await CreateRepositoryAsync(lot);
        var handler = new UploadLotImageHandler(repository, new FakeLotImageStore(), new RecordingEventPublisher());

        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream("{}"u8.ToArray());

        var result = await LotImageEndpoints.HandleUploadAsync(
            lot.LotId,
            context.Request,
            handler,
            CancellationToken.None);

        AssertValidationMessage(result, "Vedhæft billedet som en fil");
    }

    /// <summary>
    /// Verifies that the removed image is reported as no longer present.
    /// </summary>
    [Fact]
    public async Task Remove_WithStoredImage_ReturnsEmptyImageUrl()
    {
        var lot = CreateLot(imageFileName: "billede.jpg");
        var repository = await CreateRepositoryAsync(lot);
        var handler = new RemoveLotImageHandler(repository, new FakeLotImageStore(), new RecordingEventPublisher());

        var result = await LotImageEndpoints.HandleRemoveAsync(lot.LotId, handler, CancellationToken.None);

        var ok = Assert.IsType<Ok<LotImageResponse>>(result.Result);
        Assert.Null(ok.Value!.ImageUrl);
    }

    /// <summary>
    /// Verifies that removing the image of a genstand that does not exist returns 404.
    /// </summary>
    [Fact]
    public async Task Remove_WithUnknownLot_ReturnsNotFound()
    {
        var repository = await CreateRepositoryAsync();
        var handler = new RemoveLotImageHandler(repository, new FakeLotImageStore(), new RecordingEventPublisher());

        var result = await LotImageEndpoints.HandleRemoveAsync(Guid.NewGuid(), handler, CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    private static void AssertValidationMessage(
        Results<Ok<LotImageResponse>, NotFound, ValidationProblem> result,
        string expectedFragment)
    {
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains(
            problem.ProblemDetails.Errors.SelectMany(error => error.Value),
            message => message.Contains(expectedFragment));
    }

    /// <summary>
    /// Builds a multipart request body from the supplied file, or an empty form when no file is given.
    /// </summary>
    private static HttpRequest CreateMultipartRequest(byte[]? content, string? fileName, string? contentType)
    {
        using var multipart = new MultipartFormDataContent();

        if (content is not null)
        {
            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType!);
            multipart.Add(file, "file", fileName!);
        }
        else
        {
            multipart.Add(new StringContent("tom"), "beskrivelse");
        }

        var body = multipart.ReadAsByteArrayAsync().GetAwaiter().GetResult();

        var context = new DefaultHttpContext();
        context.Request.ContentType = multipart.Headers.ContentType!.ToString();
        context.Request.ContentLength = body.Length;
        context.Request.Body = new MemoryStream(body);

        return context.Request;
    }

    private static byte[] PngBytes() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];

    private static Lot CreateLot(string? imageFileName = null) => new()
    {
        LotId = Guid.NewGuid(),
        Name = "Vase",
        Category = "Keramik",
        Quantity = 1,
        EstimatedValue = 250m,
        Description = "En genstand",
        Tags = [],
        ImageFileName = imageFileName,
        AuctionHouseId = Guid.NewGuid()
    };

    private static async Task<InMemoryLotRepository> CreateRepositoryAsync(params Lot[] lots)
    {
        var repository = new InMemoryLotRepository();

        foreach (var lot in lots)
        {
            await repository.AddAsync(lot, CancellationToken.None);
        }

        return repository;
    }

    /// <summary>
    /// An image store that records what it was asked to do instead of touching the file system.
    /// </summary>
    private sealed class FakeLotImageStore : ILotImageStore
    {
        public string NextFileName { get; init; } = "genereret.png";
        public List<string> Deleted { get; } = [];

        public Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken) =>
            Task.FromResult(NextFileName);

        public Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            Deleted.Add(fileName);
            return Task.FromResult(true);
        }
    }
}
