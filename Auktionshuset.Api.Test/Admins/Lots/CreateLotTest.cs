using System.ComponentModel.DataAnnotations;
using Auktionshuset.Api.Endpoints.Admin.CreateLot;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Api.Test.Admins.Lots;

public class CreateLotTest {
    /// <summary>
    /// Verifies that a valid request returns 201 together with the location of the created lot.
    /// </summary>
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

    /// <summary>
    /// Verifies that padding is trimmed and duplicate tags are removed before the lot is saved.
    /// </summary>
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

    /// <summary>
    /// Verifies that the handler stores every command value and returns the persisted lot id.
    /// </summary>
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

    /// <summary>
    /// Verifies that the handler publishes a creation event that matches the saved lot.
    /// </summary>
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

    /// <summary>
    /// Verifies that the handler passes its cancellation token on to the repository and publisher.
    /// </summary>
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

    /// <summary>
    /// Verifies that a failing save propagates the exception and publishes no event.
    /// </summary>
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

    /// <summary>
    /// Verifies that each invalid input produces a validation error for the expected member.
    /// </summary>
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

    /// <summary>
    /// Builds a valid create-lot request, allowing individual fields to be overridden per test.
    /// </summary>
    /// <param name="name">The lot name; valid when 2 to 100 characters long.</param>
    /// <param name="category">The lot category; must not be empty or longer than 50 characters.</param>
    /// <param name="quantity">The number of items; must be at least 1.</param>
    /// <param name="estimatedValue">The estimated value; valid between 0.01 and 50,000,000.</param>
    /// <param name="description">The lot description; must not be empty or longer than 2,000 characters.</param>
    /// <param name="tags">The tags to attach; when <see langword="null"/>, a default pair of tags is used.</param>
    /// <param name="auctionHouseId">The auction house identifier as a string, so that empty or unparsable values can be supplied.</param>
    /// <returns>A request that satisfies every validation rule, with the supplied overrides applied.</returns>
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

    /// <summary>
    /// Builds a valid create-lot command from a valid request.
    /// </summary>
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

    /// <summary>
    /// Runs annotation and <see cref="IValidatableObject"/> validation on the request.
    /// </summary>
    /// <returns>Every validation result produced for the request.</returns>
    private static IReadOnlyList<ValidationResult> Validate(CreateLotRequest request) {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private sealed class RecordingLotRepository : ILotRepository {
        /// <summary>
        /// Unused by these tests; the stub only supports adding.
        /// </summary>
        public Task<bool> DeleteAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Lot? AddedLot { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        /// <summary>
        /// Records the added lot and cancellation token, returning a faulted task when the stub is
        /// configured to throw.
        /// </summary>
        public Task AddAsync(Lot lot, CancellationToken cancellationToken) {
            AddedLot = lot;
            CancellationToken = cancellationToken;
            return ExceptionToThrow is null
                ? Task.CompletedTask
                : Task.FromException(ExceptionToThrow);
        }

        /// <summary>
        /// Unused by these tests; the stub only supports adding.
        /// </summary>
        public Task<IReadOnlyList<Lot>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        /// <summary>
        /// Unused by these tests; the stub only supports adding.
        /// </summary>
        public Task<Lot?> GetByIdAsync(Guid lotId, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused by these tests; the stub only supports adding.
        /// </summary>
        public Task UpdateAsync(Lot lot, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }

    private sealed class RecordingEventPublisher : IIntegrationEventPublisher {
        public IIntegrationEvent? PublishedEvent { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Records the published event and the cancellation token it was called with.
        /// </summary>
        public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent {
            PublishedEvent = message;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}

