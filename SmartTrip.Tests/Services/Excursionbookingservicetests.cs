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

public abstract class ExcursionBookingTestBase
{
    protected readonly Mock<IRepository<ExcursionSession>> mockExcursions;
    protected readonly Mock<IExcursionReservationRepository> mockReservations;
    protected readonly Mock<IRepository<Trip>> mockTrips;
    protected readonly Mock<IAppLogger> mockLogger;
    protected readonly UserSession userSession;
    protected readonly ServiceOperations operations;
    protected readonly TripService tripService;
    protected readonly ExcursionBookingService excursionService;

    protected static DateTime Today => DateTime.Today;

    protected ExcursionBookingTestBase()
    {
        mockExcursions = new Mock<IRepository<ExcursionSession>>();
        mockReservations = new Mock<IExcursionReservationRepository>();
        mockTrips = new Mock<IRepository<Trip>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        tripService = new TripService(mockTrips.Object, userSession, operations);
        excursionService = new ExcursionBookingService(
            mockExcursions.Object,
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

    // Початок екскурсії: сьогодні + daysFromToday днів, о hour:00 (UTC).
    protected static ExcursionSession NewExcursion(
        int id,
        string city = "Lviv",
        int daysFromToday = 1,
        int hour = 10,
        int durationMinutes = 120,
        int capacity = 20,
        decimal price = 100,
        bool isActive = true)
    {
        return new ExcursionSession
        {
            ExcursionSessionId = id,
            City = city,
            StartsAt = DateTime.SpecifyKind(
                Today.AddDays(daysFromToday).AddHours(hour),
                DateTimeKind.Utc),
            DurationMinutes = durationMinutes,
            Capacity = capacity,
            PricePerPerson = price,
            IsActive = isActive
        };
    }

 
    protected void SetupExcursions(List<ExcursionSession> all)
    {
        mockExcursions
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<ExcursionSession, bool>>>()))
            .ReturnsAsync((Expression<Func<ExcursionSession, bool>> predicate) =>
                all.Where(predicate.Compile()).ToList());
    }

    protected void SetupReservedCounts(Dictionary<int, long>? counts = null)
    {
        IReadOnlyDictionary<int, long> value = counts ?? new Dictionary<int, long>();

        mockReservations
            .Setup(x => x.GetReservedCountsAsync(It.IsAny<int[]>()))
            .ReturnsAsync(value);
    }

    protected Func<ExcursionReservation?> SetupSuccessfulReservation()
    {
        ExcursionReservation? saved = null;

        mockReservations
            .Setup(x => x.TryReserveAsync(It.IsAny<ExcursionReservation>()))
            .Callback<ExcursionReservation>(r => saved = r)
            .ReturnsAsync(true);

        return () => saved;
    }
}

public class ExcursionBookingSearchTests : ExcursionBookingTestBase
{
    private readonly DateTime tomorrow = Today.AddDays(1);

    [Fact]
    public async Task Search_ReturnsActiveExcursionsOfCityOnSelectedDay_SortedByStartTime()
    {
        SetupExcursions(new List<ExcursionSession>
        {
            NewExcursion(1, hour: 16),
            NewExcursion(2, hour: 9),
            NewExcursion(3, hour: 12)
        });
        SetupReservedCounts();

        var result = await excursionService.SearchAsync("Lviv", tomorrow, 2);

        Assert.Equal(
            new[] { 2, 3, 1 },
            result.Select(x => x.Session.ExcursionSessionId).ToArray());
    }

    [Fact]
    public async Task Search_CalculatesAvailableSeats()
    {
        SetupExcursions(new List<ExcursionSession> { NewExcursion(1, capacity: 20) });
        SetupReservedCounts(new Dictionary<int, long> { [1] = 5 });

        var result = await excursionService.SearchAsync("Lviv", tomorrow, 2);

        Assert.Single(result);
        Assert.Equal(15, result[0].AvailableSeats);
    }

    [Fact]
    public async Task Search_WithoutReservations_ShowsFullCapacity()
    {
        SetupExcursions(new List<ExcursionSession> { NewExcursion(1, capacity: 12) });
        SetupReservedCounts();

        var result = await excursionService.SearchAsync("Lviv", tomorrow, 2);

        Assert.Equal(12, result[0].AvailableSeats);
    }

    [Fact]
    public async Task Search_HidesExcursionsWithoutEnoughFreeSeats()
    {
        SetupExcursions(new List<ExcursionSession>
        {
            NewExcursion(1, capacity: 10),
            NewExcursion(2, capacity: 10)
        });
        SetupReservedCounts(new Dictionary<int, long> { [1] = 8 });

        var result = await excursionService.SearchAsync("Lviv", tomorrow, 5);

        Assert.Single(result);
        Assert.Equal(2, result[0].Session.ExcursionSessionId);
    }

    [Fact]
    public async Task Search_ShowsExcursionWhenFreeSeatsEqualParticipants()
    {
        SetupExcursions(new List<ExcursionSession> { NewExcursion(1, capacity: 10) });
        SetupReservedCounts(new Dictionary<int, long> { [1] = 7 });

        var result = await excursionService.SearchAsync("Lviv", tomorrow, 3);

        Assert.Single(result);
        Assert.Equal(3, result[0].AvailableSeats);
    }

    [Fact]
    public async Task Search_MatchesCityIgnoringCaseAndSpaces()
    {
        SetupExcursions(new List<ExcursionSession>
        {
            NewExcursion(1, city: "Lviv"),
            NewExcursion(2, city: "Kyiv")
        });
        SetupReservedCounts();

        var result = await excursionService.SearchAsync("  LVIV ", tomorrow, 2);

        Assert.Single(result);
        Assert.Equal(1, result[0].Session.ExcursionSessionId);
    }

    [Fact]
    public async Task Search_DoesNotReturnInactiveExcursions()
    {
        SetupExcursions(new List<ExcursionSession>
        {
            NewExcursion(1, isActive: false),
            NewExcursion(2)
        });
        SetupReservedCounts();

        var result = await excursionService.SearchAsync("Lviv", tomorrow, 2);

        Assert.Single(result);
        Assert.Equal(2, result[0].Session.ExcursionSessionId);
    }

    [Fact]
    public async Task Search_ReturnsOnlyExcursionsOfSelectedDay()
    {
        SetupExcursions(new List<ExcursionSession>
        {
            NewExcursion(1, daysFromToday: 1),
            NewExcursion(2, daysFromToday: 2)
        });
        SetupReservedCounts();

        var result = await excursionService.SearchAsync("Lviv", tomorrow, 2);

        Assert.Single(result);
        Assert.Equal(1, result[0].Session.ExcursionSessionId);
    }

    [Fact]
    public async Task Search_DoesNotReturnExcursionsThatAlreadyStarted()
    {
        // Початок о 00:00 сьогодні — вже у минулому.
        SetupExcursions(new List<ExcursionSession>
        {
            NewExcursion(1, daysFromToday: 0, hour: 0)
        });
        SetupReservedCounts();

        var result = await excursionService.SearchAsync("Lviv", Today, 1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_SkipsExcursionsWithInvalidSettings()
    {
        SetupExcursions(new List<ExcursionSession>
        {
            NewExcursion(1, capacity: 0),
            NewExcursion(2, durationMinutes: 0),
            NewExcursion(3, durationMinutes: 721),
            NewExcursion(4, durationMinutes: 720)
        });
        SetupReservedCounts();

        var result = await excursionService.SearchAsync("Lviv", tomorrow, 1);

        Assert.Single(result);
        Assert.Equal(4, result[0].Session.ExcursionSessionId);
    }

    [Fact]
    public async Task Search_WhenNothingMatches_ReturnsEmptyList()
    {
        SetupExcursions(new List<ExcursionSession>());
        SetupReservedCounts();

        var result = await excursionService.SearchAsync("Lviv", tomorrow, 2);

        Assert.Empty(result);
    }
}

public class ExcursionBookingReserveTests : ExcursionBookingTestBase
{
    public ExcursionBookingReserveTests()
    {
        SetupOwnTrip(Today, Today.AddDays(10));
    }

    [Fact]
    public async Task Reserve_WithValidData_SavesConfirmedReservation()
    {
        mockExcursions.Setup(x => x.FindAsync(5)).ReturnsAsync(NewExcursion(5, price: 100));
        var saved = SetupSuccessfulReservation();

        var result = await excursionService.ReserveAsync(1, 5, 3, "Олена Коваль");

        Assert.NotNull(result);
        Assert.Same(saved(), result);
        Assert.Equal(1, result.TripId);
        Assert.Equal(5, result.ExcursionSessionId);
        Assert.Equal(3, result.ParticipantsCount);
        Assert.Equal("Олена Коваль", result.GuestName);
        Assert.Equal("Confirmed", result.Status);
        Assert.StartsWith("EX-", result.BookingCode);

        mockReservations.Verify(x => x.TryReserveAsync(It.IsAny<ExcursionReservation>()), Times.Once);
    }

    [Fact]
    public async Task Reserve_CalculatesTotalPriceAsPricePerPersonTimesParticipants()
    {
        mockExcursions.Setup(x => x.FindAsync(5)).ReturnsAsync(NewExcursion(5, price: 49.99m));
        SetupSuccessfulReservation();

        var result = await excursionService.ReserveAsync(1, 5, 3, "Олена Коваль");

        Assert.Equal(149.97m, result.TotalPrice);
    }

    [Fact]
    public async Task Reserve_TrimsGuestName()
    {
        mockExcursions.Setup(x => x.FindAsync(5)).ReturnsAsync(NewExcursion(5));
        SetupSuccessfulReservation();

        var result = await excursionService.ReserveAsync(1, 5, 2, "   Олена Коваль  ");

        Assert.Equal("Олена Коваль", result.GuestName);
    }

    [Fact]
    public async Task Reserve_WhenParticipantsEqualCapacity_Succeeds()
    {
        mockExcursions.Setup(x => x.FindAsync(5)).ReturnsAsync(NewExcursion(5, capacity: 15, price: 10));
        SetupSuccessfulReservation();

        var result = await excursionService.ReserveAsync(1, 5, 15, "Олена Коваль");

        Assert.Equal(15, result.ParticipantsCount);
        Assert.Equal(150m, result.TotalPrice);
    }

    [Fact]
    public async Task Reserve_ForExcursionOnLastTripDay_Succeeds()
    {
        SetupOwnTrip(Today, Today.AddDays(5));
        mockExcursions
            .Setup(x => x.FindAsync(5))
            .ReturnsAsync(NewExcursion(5, daysFromToday: 5, hour: 10, durationMinutes: 120));
        SetupSuccessfulReservation();

        var result = await excursionService.ReserveAsync(1, 5, 2, "Олена Коваль");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Reserve_ForExcursionWithMaximumDuration_Succeeds()
    {
        // 720 хв = 12 год: початок о 06:00 завершується о 18:00 того ж дня.
        mockExcursions
            .Setup(x => x.FindAsync(5))
            .ReturnsAsync(NewExcursion(5, hour: 6, durationMinutes: 720));
        SetupSuccessfulReservation();

        var result = await excursionService.ReserveAsync(1, 5, 2, "Олена Коваль");

        Assert.NotNull(result);
    }
}

public class ExcursionBookingListTests : ExcursionBookingTestBase
{
    [Fact]
    public async Task List_ReturnsReservationsOfTrip()
    {
        SetupOwnTrip(Today, Today.AddDays(10));

        var expected = new List<ExcursionReservation>
        {
            new ExcursionReservation { TripId = 1, ExcursionSessionId = 1 },
            new ExcursionReservation { TripId = 1, ExcursionSessionId = 2 }
        };

        mockReservations.Setup(x => x.ListForTripAsync(1)).ReturnsAsync(expected);

        var result = await excursionService.ListAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(1, r.TripId));
    }

    [Fact]
    public async Task List_WhenNoReservations_ReturnsEmptyList()
    {
        SetupOwnTrip(Today, Today.AddDays(10));
        mockReservations
            .Setup(x => x.ListForTripAsync(1))
            .ReturnsAsync(new List<ExcursionReservation>());

        var result = await excursionService.ListAsync(1);

        Assert.Empty(result);
    }
}