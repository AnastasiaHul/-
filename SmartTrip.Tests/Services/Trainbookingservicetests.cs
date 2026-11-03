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


public abstract class TrainBookingTestBase
{
    protected readonly Mock<IRepository<TrainDeparture>> mockDepartures;
    protected readonly Mock<ITrainReservationRepository> mockReservations;
    protected readonly Mock<IRepository<Trip>> mockTrips;
    protected readonly Mock<IAppLogger> mockLogger;
    protected readonly UserSession userSession;
    protected readonly ServiceOperations operations;
    protected readonly TripService tripService;
    protected readonly TrainBookingService trainService;

    protected static DateTime Today => DateTime.Today;

    protected TrainBookingTestBase()
    {
        mockDepartures = new Mock<IRepository<TrainDeparture>>();
        mockReservations = new Mock<ITrainReservationRepository>();
        mockTrips = new Mock<IRepository<Trip>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        tripService = new TripService(mockTrips.Object, userSession, operations);
        trainService = new TrainBookingService(
            mockDepartures.Object,
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
            City = "Kyiv",
            StartDate = start,
            EndDate = end,
            Budget = 10000,
            PeopleCount = 2
        };

        mockTrips.Setup(x => x.FindAsync(1)).ReturnsAsync(trip);
        return trip;
    }

    // Відправлення: сьогодні + daysFromToday днів, о hour:00 (UTC); 5 вагонів по 10 місць.
    protected static TrainDeparture NewDeparture(
        int id,
        string from = "Lviv",
        string to = "Kyiv",
        int daysFromToday = 1,
        int hour = 8,
        int durationHours = 6,
        int wagons = 5,
        int seatsPerWagon = 10,
        decimal price = 450)
    {
        DateTime departure = DateTime.SpecifyKind(
            Today.AddDays(daysFromToday).AddHours(hour),
            DateTimeKind.Utc);

        return new TrainDeparture
        {
            TrainDepartureId = id,
            DepartureCity = from,
            ArrivalCity = to,
            DepartureAt = departure,
            ArrivalAt = departure.AddHours(durationHours),
            WagonCount = wagons,
            SeatsPerWagon = seatsPerWagon,
            TicketPrice = price
        };
    }

 
    protected void SetupDepartures(List<TrainDeparture> all)
    {
        mockDepartures
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<TrainDeparture, bool>>>()))
            .ReturnsAsync((Expression<Func<TrainDeparture, bool>> predicate) =>
                all.Where(predicate.Compile()).ToList());
    }

    protected void SetupReservedSeats(int departureId, int wagon, params int[] seats)
    {
        mockReservations
            .Setup(x => x.ListReservedSeatsAsync(departureId, wagon))
            .ReturnsAsync(seats.ToList());
    }

    protected Func<TrainReservation?> SetupSuccessfulReservation()
    {
        TrainReservation? saved = null;

        mockReservations
            .Setup(x => x.TryReserveAsync(It.IsAny<TrainReservation>()))
            .Callback<TrainReservation>(r => saved = r)
            .ReturnsAsync(true);

        return () => saved;
    }
}

public class TrainBookingSearchTests : TrainBookingTestBase
{
    private readonly DateTime tomorrow = Today.AddDays(1);

    [Fact]
    public async Task Search_ReturnsDeparturesOfRoute_SortedByDepartureTime()
    {
        SetupDepartures(new List<TrainDeparture>
        {
            NewDeparture(1, hour: 18),
            NewDeparture(2, hour: 7),
            NewDeparture(3, hour: 12)
        });

        var result = await trainService.SearchAsync("Lviv", "Kyiv", tomorrow);

        Assert.Equal(new[] { 2, 3, 1 }, result.Select(x => x.TrainDepartureId).ToArray());
    }

    [Fact]
    public async Task Search_MatchesCitiesIgnoringCaseAndSpaces()
    {
        SetupDepartures(new List<TrainDeparture> { NewDeparture(1) });

        var result = await trainService.SearchAsync("  LVIV ", " kyiv  ", tomorrow);

        Assert.Single(result);
    }

    [Fact]
    public async Task Search_DoesNotReturnReverseDirection()
    {
        SetupDepartures(new List<TrainDeparture>
        {
            NewDeparture(1, from: "Lviv", to: "Kyiv"),
            NewDeparture(2, from: "Kyiv", to: "Lviv")
        });

        var result = await trainService.SearchAsync("Lviv", "Kyiv", tomorrow);

        Assert.Single(result);
        Assert.Equal(1, result[0].TrainDepartureId);
    }

    [Fact]
    public async Task Search_DoesNotReturnOtherRoutes()
    {
        SetupDepartures(new List<TrainDeparture>
        {
            NewDeparture(1, from: "Lviv", to: "Odesa"),
            NewDeparture(2, from: "Lviv", to: "Kyiv")
        });

        var result = await trainService.SearchAsync("Lviv", "Kyiv", tomorrow);

        Assert.Single(result);
        Assert.Equal(2, result[0].TrainDepartureId);
    }

    [Fact]
    public async Task Search_ReturnsOnlyDeparturesOfSelectedDay()
    {
        SetupDepartures(new List<TrainDeparture>
        {
            NewDeparture(1, daysFromToday: 1),
            NewDeparture(2, daysFromToday: 2)
        });

        var result = await trainService.SearchAsync("Lviv", "Kyiv", tomorrow);

        Assert.Single(result);
        Assert.Equal(1, result[0].TrainDepartureId);
    }

    [Fact]
    public async Task Search_DoesNotReturnDeparturesThatAlreadyLeft()
    {
        // Відправлення о 00:00 сьогодні — вже у минулому.
        SetupDepartures(new List<TrainDeparture>
        {
            NewDeparture(1, daysFromToday: 0, hour: 0)
        });

        var result = await trainService.SearchAsync("Lviv", "Kyiv", Today);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_WhenNothingMatches_ReturnsEmptyList()
    {
        SetupDepartures(new List<TrainDeparture>());

        var result = await trainService.SearchAsync("Lviv", "Kyiv", tomorrow);

        Assert.Empty(result);
    }
}

public class TrainBookingAvailableSeatsTests : TrainBookingTestBase
{
    [Fact]
    public async Task AvailableSeats_WithoutReservations_ReturnsAllSeatsOfWagon()
    {
        mockDepartures.Setup(x => x.FindAsync(1)).ReturnsAsync(NewDeparture(1, seatsPerWagon: 10));
        SetupReservedSeats(1, 2);

        var result = await trainService.AvailableSeatsAsync(1, 2);

        Assert.Equal(Enumerable.Range(1, 10).ToArray(), result.ToArray());
    }

    [Fact]
    public async Task AvailableSeats_ExcludesReservedSeats()
    {
        mockDepartures.Setup(x => x.FindAsync(1)).ReturnsAsync(NewDeparture(1, seatsPerWagon: 10));
        SetupReservedSeats(1, 2, 2, 5);

        var result = await trainService.AvailableSeatsAsync(1, 2);

        Assert.Equal(new[] { 1, 3, 4, 6, 7, 8, 9, 10 }, result.ToArray());
    }

    [Fact]
    public async Task AvailableSeats_WhenAllSeatsReserved_ReturnsEmptyList()
    {
        mockDepartures.Setup(x => x.FindAsync(1)).ReturnsAsync(NewDeparture(1, seatsPerWagon: 3));
        SetupReservedSeats(1, 1, 1, 2, 3);

        var result = await trainService.AvailableSeatsAsync(1, 1);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task AvailableSeats_ForFirstAndLastWagon_Works(int wagon)
    {
        mockDepartures
            .Setup(x => x.FindAsync(1))
            .ReturnsAsync(NewDeparture(1, wagons: 5, seatsPerWagon: 4));
        SetupReservedSeats(1, wagon, 1);

        var result = await trainService.AvailableSeatsAsync(1, wagon);

        Assert.Equal(new[] { 2, 3, 4 }, result.ToArray());
    }
}

public class TrainBookingReserveTests : TrainBookingTestBase
{
    public TrainBookingReserveTests()
    {
        SetupOwnTrip(Today, Today.AddDays(10));
    }

    [Fact]
    public async Task Reserve_WithValidData_SavesConfirmedReservation()
    {
        mockDepartures.Setup(x => x.FindAsync(4)).ReturnsAsync(NewDeparture(4, price: 450));
        var saved = SetupSuccessfulReservation();

        var result = await trainService.ReserveAsync(1, 4, 2, 7, "Андрій Мельник");

        Assert.NotNull(result);
        Assert.Same(saved(), result);
        Assert.Equal(1, result.TripId);
        Assert.Equal(4, result.TrainDepartureId);
        Assert.Equal(2, result.WagonNumber);
        Assert.Equal(7, result.SeatNumber);
        Assert.Equal("Андрій Мельник", result.PassengerName);
        Assert.Equal("Confirmed", result.Status);
        Assert.StartsWith("ST-", result.BookingCode);

        mockReservations.Verify(x => x.TryReserveAsync(It.IsAny<TrainReservation>()), Times.Once);
    }

    [Fact]
    public async Task Reserve_SetsTotalPriceEqualToTicketPrice()
    {
        mockDepartures.Setup(x => x.FindAsync(4)).ReturnsAsync(NewDeparture(4, price: 399.50m));
        SetupSuccessfulReservation();

        var result = await trainService.ReserveAsync(1, 4, 1, 1, "Андрій Мельник");

        Assert.Equal(399.50m, result.TotalPrice);
    }

    [Fact]
    public async Task Reserve_TrimsPassengerName()
    {
        mockDepartures.Setup(x => x.FindAsync(4)).ReturnsAsync(NewDeparture(4));
        SetupSuccessfulReservation();

        var result = await trainService.ReserveAsync(1, 4, 1, 1, "   Андрій Мельник  ");

        Assert.Equal("Андрій Мельник", result.PassengerName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Reserve_ForFirstAndLastSeat_Succeeds(int seat)
    {
        mockDepartures
            .Setup(x => x.FindAsync(4))
            .ReturnsAsync(NewDeparture(4, seatsPerWagon: 10));
        SetupSuccessfulReservation();

        var result = await trainService.ReserveAsync(1, 4, 1, seat, "Андрій Мельник");

        Assert.Equal(seat, result.SeatNumber);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task Reserve_ForFirstAndLastWagon_Succeeds(int wagon)
    {
        mockDepartures
            .Setup(x => x.FindAsync(4))
            .ReturnsAsync(NewDeparture(4, wagons: 5));
        SetupSuccessfulReservation();

        var result = await trainService.ReserveAsync(1, 4, wagon, 3, "Андрій Мельник");

        Assert.Equal(wagon, result.WagonNumber);
    }

    [Fact]
    public async Task Reserve_WhenTripLastsExactlyOneTrainDay_Succeeds()
    {
        // Відправлення о 08:00, прибуття о 14:00 того ж дня; подорож — один день.
        SetupOwnTrip(Today.AddDays(1), Today.AddDays(1));
        mockDepartures
            .Setup(x => x.FindAsync(4))
            .ReturnsAsync(NewDeparture(4, daysFromToday: 1, hour: 8, durationHours: 6));
        SetupSuccessfulReservation();

        var result = await trainService.ReserveAsync(1, 4, 1, 1, "Андрій Мельник");

        Assert.NotNull(result);
    }
}

public class TrainBookingListTests : TrainBookingTestBase
{
    [Fact]
    public async Task List_ReturnsReservationsOfTrip()
    {
        SetupOwnTrip(Today, Today.AddDays(10));

        var expected = new List<TrainReservation>
        {
            new TrainReservation { TripId = 1, TrainDepartureId = 1 },
            new TrainReservation { TripId = 1, TrainDepartureId = 2 }
        };

        mockReservations.Setup(x => x.ListForTripAsync(1)).ReturnsAsync(expected);

        var result = await trainService.ListAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(1, r.TripId));
    }

    [Fact]
    public async Task List_WhenNoReservations_ReturnsEmptyList()
    {
        SetupOwnTrip(Today, Today.AddDays(10));
        mockReservations
            .Setup(x => x.ListForTripAsync(1))
            .ReturnsAsync(new List<TrainReservation>());

        var result = await trainService.ListAsync(1);

        Assert.Empty(result);
    }
}