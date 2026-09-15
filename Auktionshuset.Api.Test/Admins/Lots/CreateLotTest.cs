using System.ComponentModel.DataAnnotations;
using Auktionshuset.Api.Endpoints.Admin.CreateLot;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Api.Test.Admins.Lots;

public class CreateLotTest {
    [Fact]
    public async Task Endpoint_WithValidRequest_ReturnsCreatedResponseWithLotLocation() {
        // Arrange
        var repository = new RecordingLotRepository();
        var handler = new CreateLotHandler(repository, new RecordingEventPublisher());

        // Act
        var result = await CreateLotEndpoint.HandleAsync(
            CreateValidRequest(),
            handler,
            CancellationToken.None);

        // Assert
        var savedLot = Assert.IsType<Lot>(repository.AddedLot);
        var response = Assert.IsType<CreateLotResponse>(result.Value);
        Assert.Equal(201, result.StatusCode);
        Assert.NotEqual(Guid.Empty, response.LotId);
        Assert.Equal(savedLot.LotId, response.LotId);
        Assert.Equal($"/api/lots/{savedLot.LotId}", result.Location);
    }

    [Fact]
    public async Task Endpoint_WithPaddedAndDuplicateValues_NormalizesRequestBeforeHandling() {
        // Arrange
        var repository = new RecordingLotRepository();
        var handler = new CreateLotHandler(repository, new RecordingEventPublisher());
        var request = CreateValidRequest(
            name: "  Antique vase  ",
            category: "  Ceramics  ",
            description: "  Hand-painted porcelain  ",
            tags: ["  antique  ", "Vase", "ANTIQUE"]);

        // Act
        await CreateLotEndpoint.HandleAsync(request, handler, CancellationToken.None);

        // Assert
        var savedLot = Assert.IsType<Lot>(repository.AddedLot);
        Assert.Equal("Antique vase", savedLot.Name);
        Assert.Equal("Ceramics", savedLot.Category);
        Assert.Equal("Hand-painted porcelain", savedLot.Description);
        Assert.Equal(["antique", "Vase"], savedLot.Tags);
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_PersistsLotAndReturnsItsId() {
        // Arrange
        var repository = new RecordingLotRepository();
        var publisher = new RecordingEventPublisher();
        var handler = new CreateLotHandler(repository, publisher);
        var command = CreateValidCommand();

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var savedLot = Assert.IsType<Lot>(repository.AddedLot);
        Assert.NotEqual(Guid.Empty, savedLot.LotId);
        Assert.Equal(savedLot.LotId, result.LotId);
        Assert.Equal(command.Name, savedLot.Name);
        Assert.Equal(command.Category, savedLot.Category);
        Assert.Equal(command.Quantity, savedLot.Quantity);
        Assert.Equal(command.EstimatedValue, savedLot.EstimatedValue);
        Assert.Equal(command.Description, savedLot.Description);
        Assert.Equal(command.Tags, savedLot.Tags);
        Assert.Equal(command.AuctionHouseId, savedLot.AuctionHouseId);
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_PublishesEventForSavedLot() {
        // Arrange
        var repository = new RecordingLotRepository();
        var publisher = new RecordingEventPublisher();
        var handler = new CreateLotHandler(repository, publisher);
        var command = CreateValidCommand();

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var savedLot = Assert.IsType<Lot>(repository.AddedLot);
        var publishedEvent = Assert.IsType<LotCreatedIntegrationEvent>(publisher.PublishedEvent);
        Assert.NotEqual(Guid.Empty, publishedEvent.EventId);
        Assert.Equal(savedLot.LotId, publishedEvent.LotId);
        Assert.Equal(savedLot.AuctionHouseId, publishedEvent.AuctionHouseId);
        Assert.Equal(savedLot.Name, publishedEvent.Name);
        Assert.Equal(savedLot.Category, publishedEvent.Category);
        Assert.Equal(savedLot.Quantity, publishedEvent.Quantity);
        Assert.Equal(savedLot.EstimatedValue, publishedEvent.EstimatedValue);
    }

    [Fact]
    public async Task HandleAsync_ForwardsCancellationTokenToRepositoryAndPublisher() {
        // Arrange
        var repository = new RecordingLotRepository();
        var publisher = new RecordingEventPublisher();
        var handler = new CreateLotHandler(repository, publisher);
        using var cancellationSource = new CancellationTokenSource();

        // Act
        await handler.HandleAsync(CreateValidCommand(), cancellationSource.Token);

        // Assert
        Assert.Equal(cancellationSource.Token, repository.CancellationToken);
        Assert.Equal(cancellationSource.Token, publisher.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_WhenSavingFails_DoesNotPublishEvent() {
        // Arrange
        var expectedException = new InvalidOperationException("The lot could not be saved.");
        var repository = new RecordingLotRepository { ExceptionToThrow = expectedException };
        var publisher = new RecordingEventPublisher();
        var handler = new CreateLotHandler(repository, publisher);

        // Act
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(CreateValidCommand(), CancellationToken.None));

        // Assert
        Assert.Same(expectedException, actualException);
        Assert.Null(publisher.PublishedEvent);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void CreateLotRequest_WithInvalidInput_FailsValidation(
        CreateLotRequest request,
        string invalidMember) {
        // Act
        var validationResults = Validate(request);

        // Assert
        Assert.Contains(validationResults, result => result.MemberNames.Contains(invalidMember));
    }

    public static TheoryData<CreateLotRequest, string> InvalidRequests => new() {
        { CreateValidRequest(name: "x"), nameof(CreateLotRequest.Name) },
        { CreateValidRequest(category: ""), nameof(CreateLotRequest.Category) },
        { CreateValidRequest(quantity: 0), nameof(CreateLotRequest.Quantity) },
        { CreateValidRequest(estimatedValue: 0), nameof(CreateLotRequest.EstimatedValue) },
        { CreateValidRequest(description: ""), nameof(CreateLotRequest.Description) },
        { CreateValidRequest(tags: Enumerable.Repeat("tag", 21).ToArray()), nameof(CreateLotRequest.Tags) },
        { CreateValidRequest(tags: ["valid", " "]), nameof(CreateLotRequest.Tags) },
        { CreateValidRequest(auctionHouseId: "00000000-0000-0000-0000-000000000000"), nameof(CreateLotRequest.AuctionHouseId) }
    };

    private static CreateLotRequest CreateValidRequest(
        string name = "Antique vase",
        string category = "Ceramics",
        int quantity = 2,
        decimal estimatedValue = 1_250.00m,
        string description = "Hand-painted porcelain",
        string[]? tags = null,
        string auctionHouseId = "53bc9a77-1e5d-47be-9446-8919ea020961") => new() {
        Name = name,
        Category = category,
        Quantity = quantity,
        EstimatedValue = estimatedValue,
        Description = description,
        Tags = tags ?? ["antique", "vase"],
        AuctionHouseId = Guid.Parse(auctionHouseId)
    };

    private static CreateLotCommand CreateValidCommand() {
        var request = CreateValidRequest();
        return new CreateLotCommand(
            request.Name,
            request.Category,
            request.Quantity,
            request.EstimatedValue,
            request.Description,
            request.Tags,
            request.AuctionHouseId);
    }

    private static IReadOnlyList<ValidationResult> Validate(CreateLotRequest request) {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private sealed class RecordingLotRepository : ILotRepository {
        public Task<bool> DeleteAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Lot? AddedLot { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public Task AddAsync(Lot lot, CancellationToken cancellationToken) {
            AddedLot = lot;
            CancellationToken = cancellationToken;
            return ExceptionToThrow is null
                ? Task.CompletedTask
                : Task.FromException(ExceptionToThrow);
        }

        public Task<IReadOnlyList<Lot>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Lot?> GetByIdAsync(Guid lotId, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(Lot lot, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }

    private sealed class RecordingEventPublisher : IIntegrationEventPublisher {
        public IIntegrationEvent? PublishedEvent { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent {
            PublishedEvent = message;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}

