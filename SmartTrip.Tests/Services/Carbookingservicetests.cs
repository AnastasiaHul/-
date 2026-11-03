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


public abstract class CarBookingTestBase
{
    protected readonly Mock<IRepository<RentalCar>> mockCars;
    protected readonly Mock<ICarReservationRepository> mockReservations;
    protected readonly Mock<IRepository<Trip>> mockTrips;
    protected readonly Mock<IAppLogger> mockLogger;
    protected readonly UserSession userSession;
    protected readonly ServiceOperations operations;
    protected readonly TripService tripService;
    protected readonly CarBookingService carService;

    protected static DateTime Today => DateTime.Today;

    protected CarBookingTestBase()
    {
        mockCars = new Mock<IRepository<RentalCar>>();
        mockReservations = new Mock<ICarReservationRepository>();
        mockTrips = new Mock<IRepository<Trip>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        tripService = new TripService(mockTrips.Object, userSession, operations);
        carService = new CarBookingService(
            mockCars.Object,
            mockReservations.Object,
            tripService,
            userSession,
            operations);

        userSession.SignIn(new User { UserId = 1, FirstName = "Tourist", Role = "Tourist" });
    }

    protected Trip SetupOwnTrip(DateTime start, DateTime end)
    {
        var trip = new Trip
        {
            TripId = 1,
            UserId = 1,
            Title = "Trip",
            City = "Lviv",
            StartDate = start,
            EndDate = end,
            Budget = 10000,
            PeopleCount = 2
        };

        mockTrips.Setup(x => x.FindAsync(1)).ReturnsAsync(trip);
        return trip;
    }

    protected static RentalCar NewCar(
        int id,
        string city = "Lviv",
        int seats = 5,
        decimal price = 50,
        bool isActive = true)
    {
        return new RentalCar
        {
            RentalCarId = id,
            City = city,
            SeatsCount = seats,
            PricePerDay = price,
            IsActive = isActive
        };
    }


    protected void SetupCars(List<RentalCar> all)
    {
        mockCars
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<RentalCar, bool>>>()))
            .ReturnsAsync((Expression<Func<RentalCar, bool>> predicate) =>
                all.Where(predicate.Compile()).ToList());
    }

    protected void SetupUnavailable(params int[] ids)
    {
        mockReservations
            .Setup(x => x.ListUnavailableIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(ids.ToList());
    }

    // Повертає функцію, яка віддає бронювання, що передав сервіс у репозиторій.
    protected Func<CarReservation?> SetupSuccessfulReservation()
    {
        CarReservation? saved = null;

        mockReservations
            .Setup(x => x.TryReserveAsync(It.IsAny<CarReservation>()))
            .Callback<CarReservation>(r => saved = r)
            .ReturnsAsync(true);

        return () => saved;
    }
}

public class CarBookingSearchTests : CarBookingTestBase
{
    private readonly DateTime pickup = Today.AddDays(10);
    private readonly DateTime back = Today.AddDays(13);

    [Fact]
    public async Task Search_ReturnsActiveCarsInCity_SortedByPrice()
    {
        SetupCars(new List<RentalCar>
        {
            NewCar(1, price: 90),
            NewCar(2, price: 40),
            NewCar(3, price: 65)
        });
        SetupUnavailable();

        var result = await carService.SearchAsync("Lviv", pickup, back, 4);

        Assert.Equal(new[] { 2, 3, 1 }, result.Select(x => x.RentalCarId).ToArray());
    }

    [Fact]
    public async Task Search_MatchesCityIgnoringCaseAndSpaces()
    {
        SetupCars(new List<RentalCar>
        {
            NewCar(1, city: "Lviv"),
            NewCar(2, city: "Kyiv")
        });
        SetupUnavailable();

        var result = await carService.SearchAsync("  lVIV  ", pickup, back, 2);

        Assert.Single(result);
        Assert.Equal(1, result[0].RentalCarId);
    }

    [Fact]
    public async Task Search_ReturnsOnlyCarsWithEnoughSeats()
    {
        SetupCars(new List<RentalCar>
        {
            NewCar(1, seats: 2),
            NewCar(2, seats: 4),
            NewCar(3, seats: 7)
        });
        SetupUnavailable();

        var result = await carService.SearchAsync("Lviv", pickup, back, 4);

        Assert.Equal(new[] { 2, 3 }, result.Select(x => x.RentalCarId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Search_DoesNotReturnInactiveCars()
    {
        SetupCars(new List<RentalCar>
        {
            NewCar(1, isActive: false),
            NewCar(2)
        });
        SetupUnavailable();

        var result = await carService.SearchAsync("Lviv", pickup, back, 2);

        Assert.Single(result);
        Assert.Equal(2, result[0].RentalCarId);
    }

    [Fact]
    public async Task Search_ExcludesCarsReservedOnSelectedDates()
    {
        SetupCars(new List<RentalCar>
        {
            NewCar(1),
            NewCar(2),
            NewCar(3)
        });
        SetupUnavailable(1, 3);

        var result = await carService.SearchAsync("Lviv", pickup, back, 2);

        Assert.Single(result);
        Assert.Equal(2, result[0].RentalCarId);
    }

    [Fact]
    public async Task Search_WhenNothingMatches_ReturnsEmptyList()
    {
        SetupCars(new List<RentalCar> { NewCar(1, city: "Kyiv") });
        SetupUnavailable();

        var result = await carService.SearchAsync("Lviv", pickup, back, 2);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_PassesDatesWithoutTimeAsUtcToReservationCheck()
    {
        SetupCars(new List<RentalCar>());

        DateTime? pickupArg = null;
        DateTime? returnArg = null;

        mockReservations
            .Setup(x => x.ListUnavailableIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<DateTime, DateTime>((p, r) =>
            {
                pickupArg = p;
                returnArg = r;
            })
            .ReturnsAsync(new List<int>());

        await carService.SearchAsync(
            "Lviv",
            pickup.AddHours(15),
            back.AddHours(9),
            2);

        Assert.Equal(pickup.Date, pickupArg);
        Assert.Equal(back.Date, returnArg);
        Assert.Equal(DateTimeKind.Utc, pickupArg!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, returnArg!.Value.Kind);
    }
}

public class CarBookingReserveTests : CarBookingTestBase
{
    private readonly DateTime pickup = Today.AddDays(10);

    public CarBookingReserveTests()
    {
        SetupOwnTrip(Today.AddDays(10), Today.AddDays(45));
    }

    [Fact]
    public async Task Reserve_WithValidData_SavesConfirmedReservation()
    {
        mockCars.Setup(x => x.FindAsync(7)).ReturnsAsync(NewCar(7, seats: 5, price: 50));
        var saved = SetupSuccessfulReservation();

        var result = await carService.ReserveAsync(
            1, 7, pickup, pickup.AddDays(3), 4, "Іван Петренко", 30);

        Assert.NotNull(result);
        Assert.Same(saved(), result);
        Assert.Equal(1, result.TripId);
        Assert.Equal(7, result.RentalCarId);
        Assert.Equal("Іван Петренко", result.DriverName);
        Assert.Equal(30, result.DriverAge);
        Assert.Equal("Confirmed", result.Status);
        Assert.StartsWith("CR-", result.BookingCode);

        mockReservations.Verify(x => x.TryReserveAsync(It.IsAny<CarReservation>()), Times.Once);
    }

    [Fact]
    public async Task Reserve_CalculatesTotalPriceAsDailyPriceTimesDays()
    {
        mockCars.Setup(x => x.FindAsync(7)).ReturnsAsync(NewCar(7, price: 85.50m));
        SetupSuccessfulReservation();

        var result = await carService.ReserveAsync(
            1, 7, pickup, pickup.AddDays(3), 2, "Іван Петренко", 30);

        Assert.Equal(256.50m, result.TotalPrice);
    }

    [Fact]
    public async Task Reserve_StoresDatesAsUtcWithoutTime()
    {
        mockCars.Setup(x => x.FindAsync(7)).ReturnsAsync(NewCar(7));
        SetupSuccessfulReservation();

        var result = await carService.ReserveAsync(
            1, 7, pickup.AddHours(14), pickup.AddDays(2).AddHours(10), 2, "Іван Петренко", 30);

        Assert.Equal(pickup.Date, result.PickupDate);
        Assert.Equal(pickup.AddDays(2).Date, result.ReturnDate);
        Assert.Equal(DateTimeKind.Utc, result.PickupDate.Kind);
        Assert.Equal(DateTimeKind.Utc, result.ReturnDate.Kind);
    }

    [Fact]
    public async Task Reserve_TrimsDriverName()
    {
        mockCars.Setup(x => x.FindAsync(7)).ReturnsAsync(NewCar(7));
        SetupSuccessfulReservation();

        var result = await carService.ReserveAsync(
            1, 7, pickup, pickup.AddDays(2), 2, "   Іван Петренко   ", 30);

        Assert.Equal("Іван Петренко", result.DriverName);
    }

    [Theory]
    [InlineData(21)]
    [InlineData(100)]
    public async Task Reserve_WithBoundaryDriverAge_Succeeds(int age)
    {
        mockCars.Setup(x => x.FindAsync(7)).ReturnsAsync(NewCar(7));
        SetupSuccessfulReservation();

        var result = await carService.ReserveAsync(
            1, 7, pickup, pickup.AddDays(2), 2, "Іван Петренко", age);

        Assert.Equal(age, result.DriverAge);
    }

    [Fact]
    public async Task Reserve_WhenRequiredSeatsEqualCarSeats_Succeeds()
    {
        mockCars.Setup(x => x.FindAsync(7)).ReturnsAsync(NewCar(7, seats: 5));
        SetupSuccessfulReservation();

        var result = await carService.ReserveAsync(
            1, 7, pickup, pickup.AddDays(2), 5, "Іван Петренко", 30);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Reserve_ForThirtyDays_Succeeds()
    {
        mockCars.Setup(x => x.FindAsync(7)).ReturnsAsync(NewCar(7, price: 10));
        SetupSuccessfulReservation();

        var result = await carService.ReserveAsync(
            1, 7, pickup, pickup.AddDays(30), 2, "Іван Петренко", 30);

        Assert.Equal(300m, result.TotalPrice);
    }

    [Fact]
    public async Task Reserve_FromFirstToLastDayOfTrip_Succeeds()
    {
        SetupOwnTrip(Today.AddDays(10), Today.AddDays(15));
        mockCars.Setup(x => x.FindAsync(7)).ReturnsAsync(NewCar(7, price: 20));
        SetupSuccessfulReservation();

        var result = await carService.ReserveAsync(
            1, 7, Today.AddDays(10), Today.AddDays(15), 2, "Іван Петренко", 30);

        Assert.Equal(100m, result.TotalPrice);
    }
}

public class CarBookingListTests : CarBookingTestBase
{
    [Fact]
    public async Task List_ReturnsReservationsOfTrip()
    {
        SetupOwnTrip(Today.AddDays(10), Today.AddDays(20));

        var expected = new List<CarReservation>
        {
            new CarReservation { TripId = 1, RentalCarId = 1 },
            new CarReservation { TripId = 1, RentalCarId = 2 }
        };

        mockReservations.Setup(x => x.ListForTripAsync(1)).ReturnsAsync(expected);

        var result = await carService.ListAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(1, r.TripId));
    }

    [Fact]
    public async Task List_WhenNoReservations_ReturnsEmptyList()
    {
        SetupOwnTrip(Today.AddDays(10), Today.AddDays(20));
        mockReservations.Setup(x => x.ListForTripAsync(1)).ReturnsAsync(new List<CarReservation>());

        var result = await carService.ListAsync(1);

        Assert.Empty(result);
    }
}