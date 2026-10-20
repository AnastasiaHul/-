using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;
using Moq;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Tests.Services;

public class TripServiceCreateTripTests
{
    private readonly Mock<IRepository<Trip>> mockTrips;
    private readonly Mock<IAppLogger> mockLogger;
    private readonly UserSession userSession;
    private readonly ServiceOperations operations;
    private readonly TripService tripService;

    public TripServiceCreateTripTests()
    {
        mockTrips = new Mock<IRepository<Trip>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        tripService = new TripService(mockTrips.Object, userSession, operations);
        
        var currentUser = new User { UserId = 1, FirstName = "Tourist", Role = "Tourist" };
        userSession.SignIn(currentUser);
    }

    [Fact]
    public async Task CreateTrip_WithValidData_CreatesTrip()
    {
        mockTrips
            .Setup(x => x.AddAsync(It.IsAny<Trip>()))
            .Returns(Task.CompletedTask);

        var result = await tripService.SaveAsync(
            id: null,
            title: "Paris Trip",
            city: "Paris",
            start: new DateTime(2024, 10, 1),
            end: new DateTime(2024, 10, 10),
            budget: 5000,
            people: 4,
            interests: "Culture, History");

        Assert.NotNull(result);
        Assert.Equal("Paris Trip", result.Title);
        Assert.Equal("Paris", result.City);
        Assert.Equal(1, result.UserId);

        mockTrips.Verify(
            x => x.AddAsync(It.IsAny<Trip>()),
            Times.Once,
            "Trip must be added to repository");
    }

    [Fact]
    public async Task CreateTrip_WithEmptyTitle_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => tripService.SaveAsync(
                null, "", "Paris",
                new DateTime(2024, 10, 1),
                new DateTime(2024, 10, 10),
                5000, 4, ""));

        Assert.Contains("Назва", exception.Message);
        
        mockTrips.Verify(
            x => x.AddAsync(It.IsAny<Trip>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateTrip_WithEndBeforeStart_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => tripService.SaveAsync(
                null, "Paris", "Paris",
                new DateTime(2024, 10, 10),
                new DateTime(2024, 10, 1),
                5000, 4, ""));

        Assert.Contains("раніше початку", exception.Message);
        
        mockTrips.Verify(
            x => x.AddAsync(It.IsAny<Trip>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateTrip_With366Days_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => tripService.SaveAsync(
                null, "Long Trip", "Paris",
                new DateTime(2024, 1, 1),
                new DateTime(2025, 1, 2),
                5000, 4, ""));

        Assert.Contains("365 днів", exception.Message);
        
        mockTrips.Verify(
            x => x.AddAsync(It.IsAny<Trip>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateTrip_With365Days_Succeeds()
    {
        mockTrips
            .Setup(x => x.AddAsync(It.IsAny<Trip>()))
            .Returns(Task.CompletedTask);

        var result = await tripService.SaveAsync(
            null, "Long Trip", "Paris",
            new DateTime(2024, 1, 1),
            new DateTime(2024, 12, 31),
            5000, 4, "");

        Assert.NotNull(result);
        mockTrips.Verify(x => x.AddAsync(It.IsAny<Trip>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateTrip_WithInvalidPeopleCount_ThrowsException(int peopleCount)
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => tripService.SaveAsync(
                null, "Trip", "City",
                new DateTime(2024, 10, 1),
                new DateTime(2024, 10, 10),
                5000, peopleCount, ""));

        Assert.Contains("від 1 до 1000", exception.Message);
        
        mockTrips.Verify(x => x.AddAsync(It.IsAny<Trip>()), Times.Never);
    }

    [Fact]
    public async Task CreateTrip_WithPeopleCountExceeding1000_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => tripService.SaveAsync(
                null, "Trip", "City",
                new DateTime(2024, 10, 1),
                new DateTime(2024, 10, 10),
                5000, 1001, ""));

        Assert.Contains("від 1 до 1000", exception.Message);
        
        mockTrips.Verify(x => x.AddAsync(It.IsAny<Trip>()), Times.Never);
    }

    [Fact]
    public async Task CreateTrip_WithInterestsExceeding1000Chars_ThrowsException()
    {
        string longInterests = new string('A', 1001);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => tripService.SaveAsync(
                null, "Trip", "City",
                new DateTime(2024, 10, 1),
                new DateTime(2024, 10, 10),
                5000, 4, longInterests));

        Assert.Contains("1000 символів", exception.Message);
        
        mockTrips.Verify(x => x.AddAsync(It.IsAny<Trip>()), Times.Never);
    }

    [Fact]
    public async Task CreateTrip_SetsUserIdFromSession()
    {
        int userId = userSession.CurrentUser!.UserId;
        mockTrips
            .Setup(x => x.AddAsync(It.IsAny<Trip>()))
            .Returns(Task.CompletedTask);

        var result = await tripService.SaveAsync(
            null, "Trip", "City",
            new DateTime(2024, 10, 1),
            new DateTime(2024, 10, 10),
            5000, 4, "");

        Assert.Equal(userId, result.UserId);
    }
}

public class TripServiceEditTripTests
{
    private readonly Mock<IRepository<Trip>> mockTrips;
    private readonly Mock<IAppLogger> mockLogger;
    private readonly UserSession userSession;
    private readonly ServiceOperations operations;
    private readonly TripService tripService;

    public TripServiceEditTripTests()
    {
        mockTrips = new Mock<IRepository<Trip>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        tripService = new TripService(mockTrips.Object, userSession, operations);

        var currentUser = new User { UserId = 1, FirstName = "Tourist", Role = "Tourist" };
        userSession.SignIn(currentUser);
    }

    [Fact]
    public async Task EditTrip_WithOwnTrip_UpdatesSuccessfully()
    {
        var ownTrip = new Trip
        {
            TripId = 1,
            UserId = 1,
            Title = "Old Title",
            City = "Old City",
            StartDate = new DateTime(2024, 10, 1),
            EndDate = new DateTime(2024, 10, 10),
            Budget = 1000,
            PeopleCount = 2
        };

        mockTrips
            .Setup(x => x.FindAsync(1))
            .ReturnsAsync(ownTrip);

        mockTrips
            .Setup(x => x.UpdateAsync(It.IsAny<Trip>()))
            .Returns(Task.CompletedTask);

        var result = await tripService.SaveAsync(
            id: 1,
            title: "New Title",
            city: "New City",
            start: new DateTime(2024, 10, 1),
            end: new DateTime(2024, 10, 10),
            budget: 2000,
            people: 3,
            interests: "");

        Assert.Equal("New Title", result.Title);
        Assert.Equal("New City", result.City);
        Assert.Equal(2000, result.Budget);

        mockTrips.Verify(
            x => x.UpdateAsync(It.IsAny<Trip>()),
            Times.Once);
    }

    [Fact]
    public async Task EditTrip_WithOtherUserTrip_ThrowsException()
    {
        var otherUserTrip = new Trip
        {
            TripId = 1,
            UserId = 999,
            Title = "Other User's Trip"
        };

        mockTrips
            .Setup(x => x.FindAsync(1))
            .ReturnsAsync(otherUserTrip);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tripService.SaveAsync(
                1, "Title", "City",
                new DateTime(2024, 10, 1),
                new DateTime(2024, 10, 10),
                5000, 4, ""));

        Assert.Contains("власними подорожами", exception.Message);

        mockTrips.Verify(
            x => x.UpdateAsync(It.IsAny<Trip>()),
            Times.Never);
    }
}

public class TripServiceListTripsTests
{
    private readonly Mock<IRepository<Trip>> mockTrips;
    private readonly Mock<IAppLogger> mockLogger;
    private readonly UserSession userSession;
    private readonly ServiceOperations operations;
    private readonly TripService tripService;

    public TripServiceListTripsTests()
    {
        mockTrips = new Mock<IRepository<Trip>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        tripService = new TripService(mockTrips.Object, userSession, operations);

        var currentUser = new User { UserId = 1, FirstName = "Tourist", Role = "Tourist" };
        userSession.SignIn(currentUser);
    }

    [Fact]
    public async Task ListTrips_ReturnsOnlyCurrentUserTrips()
    {
        var userTrips = new List<Trip>
        {
            new Trip { TripId = 1, UserId = 1, Title = "My Trip 1" },
            new Trip { TripId = 3, UserId = 1, Title = "My Trip 2" }
        };

        mockTrips
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<Trip, bool>>>()))
            .ReturnsAsync(userTrips);

        var result = await tripService.ListAsync();

        Assert.Equal(2, result.Count);
        Assert.All(result, trip => Assert.Equal(1, trip.UserId));

        mockTrips.Verify(
            x => x.ListAsync(It.IsAny<Expression<Func<Trip, bool>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ListTrips_ReturnsEmptyWhenUserHasNoTrips()
    {
        mockTrips
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<Trip, bool>>>()))
            .ReturnsAsync(new List<Trip>());

        var result = await tripService.ListAsync();

        Assert.Empty(result);
    }
}

public class TripServiceDeleteTripTests
{
    private readonly Mock<IRepository<Trip>> mockTrips;
    private readonly Mock<IAppLogger> mockLogger;
    private readonly UserSession userSession;
    private readonly ServiceOperations operations;
    private readonly TripService tripService;

    public TripServiceDeleteTripTests()
    {
        mockTrips = new Mock<IRepository<Trip>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        tripService = new TripService(mockTrips.Object, userSession, operations);

        var currentUser = new User { UserId = 1, FirstName = "Tourist", Role = "Tourist" };
        userSession.SignIn(currentUser);
    }

    [Fact]
    public async Task DeleteTrip_WithOwnTrip_DeletesSuccessfully()
    {
        var ownTrip = new Trip { TripId = 1, UserId = 1, Title = "My Trip" };

        mockTrips
            .Setup(x => x.FindAsync(1))
            .ReturnsAsync(ownTrip);

        mockTrips
            .Setup(x => x.DeleteAsync(It.IsAny<Trip>()))
            .Returns(Task.CompletedTask);

        await tripService.DeleteAsync(1);

        mockTrips.Verify(
            x => x.DeleteAsync(It.IsAny<Trip>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteTrip_WithOtherUserTrip_ThrowsException()
    {
        var otherUserTrip = new Trip { TripId = 1, UserId = 999, Title = "Other Trip" };

        mockTrips
            .Setup(x => x.FindAsync(1))
            .ReturnsAsync(otherUserTrip);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tripService.DeleteAsync(1));

        Assert.Contains("власними подорожами", exception.Message);

        mockTrips.Verify(
            x => x.DeleteAsync(It.IsAny<Trip>()),
            Times.Never);
    }
}