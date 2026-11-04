using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using Xunit;

namespace SmartTrip.Tests.Services;

public class ResultMigrationTests
{
    private readonly UserSession session = new();
    private readonly Mock<IAppLogger> logger = new();
    private readonly Mock<IRepository<Trip>> tripRepository = new();
    private readonly ServiceOperations operations;
    private readonly TripService trips;
    private static DateTime Tomorrow => DateTime.Today.AddDays(1);

    public ResultMigrationTests()
    {
        operations = new ServiceOperations(logger.Object, session);
        trips = new TripService(tripRepository.Object, session, operations);
        tripRepository.Setup(x => x.FindAsync(1)).ReturnsAsync(new Trip
        {
            TripId = 1,
            UserId = 1,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(5),
            PeopleCount = 2,
            Budget = 10000m
        });
    }

    [Theory]
    [InlineData("Trip")]
    [InlineData("Place")]
    [InlineData("Budget")]
    [InlineData("Packing")]
    [InlineData("Itinerary")]
    [InlineData("Map")]
    [InlineData("Train")]
    [InlineData("Housing")]
    [InlineData("Car")]
    [InlineData("Excursion")]
    public async Task Guest_ReturnsFailureWithoutThrowing(string service)
    {
        var places = new Mock<IRepository<Place>>();
        var itinerary = new ItineraryService(
            new Mock<IRepository<ItineraryVisit>>().Object,
            places.Object, trips, operations);

        Result result = service switch
        {
            "Trip" => FailureOf(await trips.ListAsync()),
            "Place" => FailureOf(await new PlaceService(places.Object, session, operations).ListOwnAsync()),
            "Budget" => FailureOf(await new BudgetService(new Mock<IRepository<Expense>>().Object, places.Object, trips, operations).CalculateAsync(1)),
            "Packing" => FailureOf(await new PackingService(new Mock<IRepository<PackingItem>>().Object, trips, operations).ListAsync(1)),
            "Itinerary" => FailureOf(await itinerary.ListAsync(1)),
            "Map" => FailureOf(await new MapRouteService(places.Object, trips, itinerary, operations).BuildUrlAsync(1, Tomorrow, "walking")),
            "Train" => FailureOf(await new TrainBookingService(new Mock<IRepository<TrainDeparture>>().Object, new Mock<ITrainReservationRepository>().Object, trips, session, operations).SearchAsync("Львів", "Київ", Tomorrow)),
            "Housing" => FailureOf(await new AccommodationBookingService(new Mock<IRepository<Accommodation>>().Object, new Mock<IAccommodationReservationRepository>().Object, trips, session, operations).SearchAsync("Київ", Tomorrow, Tomorrow.AddDays(1), 1)),
            "Car" => FailureOf(await new CarBookingService(new Mock<IRepository<RentalCar>>().Object, new Mock<ICarReservationRepository>().Object, trips, session, operations).SearchAsync("Київ", Tomorrow, Tomorrow.AddDays(1), 1)),
            "Excursion" => FailureOf(await new ExcursionBookingService(new Mock<IRepository<ExcursionSession>>().Object, new Mock<IExcursionReservationRepository>().Object, trips, session, operations).SearchAsync("Київ", Tomorrow, 1)),
            _ => throw new InvalidOperationException("Unknown test case")
        };

        Assert.True(result.IsFailure);
        Assert.Contains("увійдіть", result.Error);
        logger.Verify(x => x.Information(
            It.Is<string>(message => message.Contains("ValidationFailed")),
            It.IsAny<int?>()), Times.AtLeastOnce);
        logger.Verify(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task Packing_DuplicateName_DoesNotSave()
    {
        SignIn();
        var items = new Mock<IRepository<PackingItem>>();
        items.Setup(x => x.ListAsync(It.IsAny<Expression<Func<PackingItem, bool>>>()))
            .ReturnsAsync(new List<PackingItem> { new() { TripId = 1, Name = "Паспорт" } });
        var service = new PackingService(items.Object, trips, operations);

        Result result = await service.AddAsync(1, "паспорт");

        Assert.True(result.IsFailure);
        Assert.Contains("уже", result.Error);
        items.Verify(x => x.AddAsync(It.IsAny<PackingItem>()), Times.Never);
    }

    [Fact]
    public async Task Train_SeatConflict_ReturnsFailure()
    {
        SignIn();
        var departures = new Mock<IRepository<TrainDeparture>>();
        departures.Setup(x => x.FindAsync(1)).ReturnsAsync(new TrainDeparture
        {
            TrainDepartureId = 1,
            DepartureAt = Tomorrow.AddHours(10),
            ArrivalAt = Tomorrow.AddHours(17),
            WagonCount = 3,
            SeatsPerWagon = 20,
            TicketPrice = 600m
        });
        var reservations = new Mock<ITrainReservationRepository>();
        reservations.Setup(x => x.TryReserveAsync(It.IsAny<TrainReservation>())).ReturnsAsync(false);
        var service = new TrainBookingService(departures.Object, reservations.Object, trips, session, operations);

        Result<TrainReservation> result = await service.ReserveAsync(1, 1, 1, 1, "Test Guest");

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Contains("заброньовано", result.Error);
        reservations.Verify(x => x.TryReserveAsync(It.Is<TrainReservation>(r => r.TotalPrice == 600m)), Times.Once);
    }

    [Fact]
    public async Task Housing_Overlap_ReturnsFailure()
    {
        SignIn();
        var homes = new Mock<IRepository<Accommodation>>();
        homes.Setup(x => x.FindAsync(1)).ReturnsAsync(new Accommodation
        {
            AccommodationId = 1, IsActive = true, MaxGuests = 4, PricePerNight = 1200m
        });
        var reservations = new Mock<IAccommodationReservationRepository>();
        reservations.Setup(x => x.TryReserveAsync(It.IsAny<AccommodationReservation>())).ReturnsAsync(false);
        var service = new AccommodationBookingService(homes.Object, reservations.Object, trips, session, operations);

        Result<AccommodationReservation> result = await service.ReserveAsync(1, 1, Tomorrow, Tomorrow.AddDays(2), 2, "Test Guest");

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        reservations.Verify(x => x.TryReserveAsync(It.Is<AccommodationReservation>(r => r.TotalPrice == 2400m)), Times.Once);
    }

    [Theory]
    [InlineData(20, false)]
    [InlineData(21, true)]
    public async Task Car_ValidatesMinimumDriverAge(int age, bool expectedSuccess)
    {
        SignIn();
        var cars = new Mock<IRepository<RentalCar>>();
        cars.Setup(x => x.FindAsync(1)).ReturnsAsync(new RentalCar
        {
            RentalCarId = 1, IsActive = true, SeatsCount = 5, PricePerDay = 1400m
        });
        var reservations = new Mock<ICarReservationRepository>();
        reservations.Setup(x => x.TryReserveAsync(It.IsAny<CarReservation>())).ReturnsAsync(true);
        var service = new CarBookingService(cars.Object, reservations.Object, trips, session, operations);

        Result<CarReservation> result = await service.ReserveAsync(1, 1, Tomorrow, Tomorrow.AddDays(2), 2, "Test Driver", age);

        Assert.Equal(expectedSuccess, result.IsSuccess);
        if (expectedSuccess)
        {
            Assert.Equal(2800m, Assert.IsType<CarReservation>(result.Value).TotalPrice);
            reservations.Verify(x => x.TryReserveAsync(It.IsAny<CarReservation>()), Times.Once);
        }
        else
        {
            Assert.Contains("21", result.Error);
            reservations.Verify(x => x.TryReserveAsync(It.IsAny<CarReservation>()), Times.Never);
        }
    }

    [Fact]
    public async Task Excursion_CapacityConflict_ReturnsFailure()
    {
        SignIn();
        var excursions = new Mock<IRepository<ExcursionSession>>();
        excursions.Setup(x => x.FindAsync(1)).ReturnsAsync(new ExcursionSession
        {
            ExcursionSessionId = 1, IsActive = true, StartsAt = Tomorrow.AddHours(10),
            DurationMinutes = 120, Capacity = 10, PricePerPerson = 350m
        });
        var reservations = new Mock<IExcursionReservationRepository>();
        reservations.Setup(x => x.TryReserveAsync(It.IsAny<ExcursionReservation>())).ReturnsAsync(false);
        var service = new ExcursionBookingService(excursions.Object, reservations.Object, trips, session, operations);

        Result<ExcursionReservation> result = await service.ReserveAsync(1, 1, 2, "Test Guest");

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        reservations.Verify(x => x.TryReserveAsync(It.Is<ExcursionReservation>(r => r.TotalPrice == 700m && r.ParticipantsCount == 2)), Times.Once);
    }

    [Fact]
    public async Task Itinerary_OutsideTrip_DoesNotSave()
    {
        SignIn();
        var visits = new Mock<IRepository<ItineraryVisit>>();
        var service = new ItineraryService(visits.Object, new Mock<IRepository<Place>>().Object, trips, operations);

        Result result = await service.AddAsync(1, 1, DateTime.Today.AddDays(10), "");

        Assert.True(result.IsFailure);
        Assert.Contains("межах подорожі", result.Error);
        visits.Verify(x => x.AddAsync(It.IsAny<ItineraryVisit>()), Times.Never);
    }

    [Fact]
    public async Task TechnicalFailure_IsLoggedAndRethrown()
    {
        SignIn();
        var failure = new InvalidOperationException("Database offline");
        tripRepository.Setup(x => x.FindAsync(1)).ThrowsAsync(failure);
        var service = new BudgetService(new Mock<IRepository<Expense>>().Object, new Mock<IRepository<Place>>().Object, trips, operations);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CalculateAsync(1));

        Assert.Same(failure, actual);
        logger.Verify(x => x.Error(failure, "CalculateBudget", 1), Times.Once);
    }

    private void SignIn() => session.SignIn(new User { UserId = 1, Role = "Tourist", FirstName = "Test" });

    private static Result FailureOf<T>(Result<T> result)
    {
        Assert.True(result.IsFailure, result.Error);
        return Result.Failure(result.Error);
    }
}
