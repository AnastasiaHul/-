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

public abstract class AccommodationBookingTestBase
{
    protected readonly Mock<IRepository<Accommodation>> mockAccommodations;
    protected readonly Mock<IAccommodationReservationRepository> mockReservations;
    protected readonly Mock<IRepository<Trip>> mockTrips;
    protected readonly Mock<IAppLogger> mockLogger;
    protected readonly UserSession userSession;
    protected readonly ServiceOperations operations;
    protected readonly TripService tripService;
    protected readonly AccommodationBookingService accommodationService;

    protected static DateTime Today => DateTime.Today;

    protected AccommodationBookingTestBase()
    {
        mockAccommodations = new Mock<IRepository<Accommodation>>();
        mockReservations = new Mock<IAccommodationReservationRepository>();
        mockTrips = new Mock<IRepository<Trip>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        tripService = new TripService(mockTrips.Object, userSession, operations);
        accommodationService = new AccommodationBookingService(
            mockAccommodations.Object,
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

    protected static Accommodation NewAccommodation(
        int id,
        string city = "Lviv",
        int maxGuests = 4,
        decimal price = 100,
        bool isActive = true)
    {
        return new Accommodation
        {
            AccommodationId = id,
            City = city,
            MaxGuests = maxGuests,
            PricePerNight = price,
            IsActive = isActive
        };
    }

   
    protected void SetupAccommodations(List<Accommodation> all)
    {
        mockAccommodations
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<Accommodation, bool>>>()))
            .ReturnsAsync((Expression<Func<Accommodation, bool>> predicate) =>
                all.Where(predicate.Compile()).ToList());
    }

    protected void SetupUnavailable(params int[] ids)
    {
        mockReservations
            .Setup(x => x.ListUnavailableIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(ids.ToList());
    }

    protected Func<AccommodationReservation?> SetupSuccessfulReservation()
    {
        AccommodationReservation? saved = null;

        mockReservations
            .Setup(x => x.TryReserveAsync(It.IsAny<AccommodationReservation>()))
            .Callback<AccommodationReservation>(r => saved = r)
            .ReturnsAsync(true);

        return () => saved;
    }
}

public class AccommodationBookingSearchTests : AccommodationBookingTestBase
{
    private readonly DateTime checkIn = Today.AddDays(10);
    private readonly DateTime checkOut = Today.AddDays(13);

    [Fact]
    public async Task Search_ReturnsActiveAccommodationInCity_SortedByPrice()
    {
        SetupAccommodations(new List<Accommodation>
        {
            NewAccommodation(1, price: 180),
            NewAccommodation(2, price: 75),
            NewAccommodation(3, price: 120)
        });
        SetupUnavailable();

        var result = await accommodationService.SearchAsync("Lviv", checkIn, checkOut, 2);

        Assert.Equal(new[] { 2, 3, 1 }, result.Select(x => x.AccommodationId).ToArray());
    }

    [Fact]
    public async Task Search_MatchesCityIgnoringCaseAndSpaces()
    {
        SetupAccommodations(new List<Accommodation>
        {
            NewAccommodation(1, city: "Lviv"),
            NewAccommodation(2, city: "Kyiv")
        });
        SetupUnavailable();

        var result = await accommodationService.SearchAsync("  lviv ", checkIn, checkOut, 2);

        Assert.Single(result);
        Assert.Equal(1, result[0].AccommodationId);
    }

    [Fact]
    public async Task Search_ReturnsOnlyAccommodationThatFitsGuests()
    {
        SetupAccommodations(new List<Accommodation>
        {
            NewAccommodation(1, maxGuests: 2),
            NewAccommodation(2, maxGuests: 4),
            NewAccommodation(3, maxGuests: 6)
        });
        SetupUnavailable();

        var result = await accommodationService.SearchAsync("Lviv", checkIn, checkOut, 4);

        Assert.Equal(
            new[] { 2, 3 },
            result.Select(x => x.AccommodationId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Search_DoesNotReturnInactiveAccommodation()
    {
        SetupAccommodations(new List<Accommodation>
        {
            NewAccommodation(1, isActive: false),
            NewAccommodation(2)
        });
        SetupUnavailable();

        var result = await accommodationService.SearchAsync("Lviv", checkIn, checkOut, 2);

        Assert.Single(result);
        Assert.Equal(2, result[0].AccommodationId);
    }

    [Fact]
    public async Task Search_ExcludesAccommodationReservedOnSelectedDates()
    {
        SetupAccommodations(new List<Accommodation>
        {
            NewAccommodation(1),
            NewAccommodation(2),
            NewAccommodation(3)
        });
        SetupUnavailable(1, 3);

        var result = await accommodationService.SearchAsync("Lviv", checkIn, checkOut, 2);

        Assert.Single(result);
        Assert.Equal(2, result[0].AccommodationId);
    }

    [Fact]
    public async Task Search_WhenNothingMatches_ReturnsEmptyList()
    {
        SetupAccommodations(new List<Accommodation> { NewAccommodation(1, city: "Kyiv") });
        SetupUnavailable();

        var result = await accommodationService.SearchAsync("Lviv", checkIn, checkOut, 2);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_PassesDatesWithoutTimeAsUtcToReservationCheck()
    {
        SetupAccommodations(new List<Accommodation>());

        DateTime? checkInArg = null;
        DateTime? checkOutArg = null;

        mockReservations
            .Setup(x => x.ListUnavailableIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<DateTime, DateTime>((a, b) =>
            {
                checkInArg = a;
                checkOutArg = b;
            })
            .ReturnsAsync(new List<int>());

        await accommodationService.SearchAsync(
            "Lviv",
            checkIn.AddHours(14),
            checkOut.AddHours(11),
            2);

        Assert.Equal(checkIn.Date, checkInArg);
        Assert.Equal(checkOut.Date, checkOutArg);
        Assert.Equal(DateTimeKind.Utc, checkInArg!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, checkOutArg!.Value.Kind);
    }
}

public class AccommodationBookingReserveTests : AccommodationBookingTestBase
{
    private readonly DateTime checkIn = Today.AddDays(10);

    public AccommodationBookingReserveTests()
    {
        SetupOwnTrip(Today.AddDays(10), Today.AddDays(45));
    }

    [Fact]
    public async Task Reserve_WithValidData_SavesConfirmedReservation()
    {
        mockAccommodations
            .Setup(x => x.FindAsync(3))
            .ReturnsAsync(NewAccommodation(3, maxGuests: 4, price: 100));
        var saved = SetupSuccessfulReservation();

        var result = await accommodationService.ReserveAsync(
            1, 3, checkIn, checkIn.AddDays(3), 2, "Марія Шевченко");

        Assert.NotNull(result);
        Assert.Same(saved(), result);
        Assert.Equal(1, result.TripId);
        Assert.Equal(3, result.AccommodationId);
        Assert.Equal(2, result.GuestsCount);
        Assert.Equal("Марія Шевченко", result.GuestName);
        Assert.Equal("Confirmed", result.Status);
        Assert.StartsWith("HT-", result.BookingCode);

        mockReservations.Verify(
            x => x.TryReserveAsync(It.IsAny<AccommodationReservation>()),
            Times.Once);
    }

    [Fact]
    public async Task Reserve_CalculatesTotalPriceAsNightlyPriceTimesNights()
    {
        mockAccommodations
            .Setup(x => x.FindAsync(3))
            .ReturnsAsync(NewAccommodation(3, price: 89.90m));
        SetupSuccessfulReservation();

        var result = await accommodationService.ReserveAsync(
            1, 3, checkIn, checkIn.AddDays(4), 2, "Марія Шевченко");

        Assert.Equal(359.60m, result.TotalPrice);
    }

    [Fact]
    public async Task Reserve_StoresDatesAsUtcWithoutTime()
    {
        mockAccommodations.Setup(x => x.FindAsync(3)).ReturnsAsync(NewAccommodation(3));
        SetupSuccessfulReservation();

        var result = await accommodationService.ReserveAsync(
            1, 3, checkIn.AddHours(15), checkIn.AddDays(2).AddHours(11), 2, "Марія Шевченко");

        Assert.Equal(checkIn.Date, result.CheckIn);
        Assert.Equal(checkIn.AddDays(2).Date, result.CheckOut);
        Assert.Equal(DateTimeKind.Utc, result.CheckIn.Kind);
        Assert.Equal(DateTimeKind.Utc, result.CheckOut.Kind);
    }

    [Fact]
    public async Task Reserve_TrimsGuestName()
    {
        mockAccommodations.Setup(x => x.FindAsync(3)).ReturnsAsync(NewAccommodation(3));
        SetupSuccessfulReservation();

        var result = await accommodationService.ReserveAsync(
            1, 3, checkIn, checkIn.AddDays(2), 2, "   Марія Шевченко  ");

        Assert.Equal("Марія Шевченко", result.GuestName);
    }

    [Fact]
    public async Task Reserve_WhenGuestsEqualMaxGuests_Succeeds()
    {
        mockAccommodations
            .Setup(x => x.FindAsync(3))
            .ReturnsAsync(NewAccommodation(3, maxGuests: 4));
        SetupSuccessfulReservation();

        var result = await accommodationService.ReserveAsync(
            1, 3, checkIn, checkIn.AddDays(2), 4, "Марія Шевченко");

        Assert.Equal(4, result.GuestsCount);
    }

    [Fact]
    public async Task Reserve_ForThirtyNights_Succeeds()
    {
        mockAccommodations
            .Setup(x => x.FindAsync(3))
            .ReturnsAsync(NewAccommodation(3, price: 10));
        SetupSuccessfulReservation();

        var result = await accommodationService.ReserveAsync(
            1, 3, checkIn, checkIn.AddDays(30), 2, "Марія Шевченко");

        Assert.Equal(300m, result.TotalPrice);
    }

    [Fact]
    public async Task Reserve_FromFirstToLastDayOfTrip_Succeeds()
    {
        SetupOwnTrip(Today.AddDays(10), Today.AddDays(15));
        mockAccommodations
            .Setup(x => x.FindAsync(3))
            .ReturnsAsync(NewAccommodation(3, price: 20));
        SetupSuccessfulReservation();

        var result = await accommodationService.ReserveAsync(
            1, 3, Today.AddDays(10), Today.AddDays(15), 2, "Марія Шевченко");

        Assert.Equal(100m, result.TotalPrice);
    }
}

public class AccommodationBookingListTests : AccommodationBookingTestBase
{
    [Fact]
    public async Task List_ReturnsReservationsOfTrip()
    {
        SetupOwnTrip(Today.AddDays(10), Today.AddDays(20));

        var expected = new List<AccommodationReservation>
        {
            new AccommodationReservation { TripId = 1, AccommodationId = 1 },
            new AccommodationReservation { TripId = 1, AccommodationId = 2 }
        };

        mockReservations.Setup(x => x.ListForTripAsync(1)).ReturnsAsync(expected);

        var result = await accommodationService.ListAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(1, r.TripId));
    }

    [Fact]
    public async Task List_WhenNoReservations_ReturnsEmptyList()
    {
        SetupOwnTrip(Today.AddDays(10), Today.AddDays(20));
        mockReservations
            .Setup(x => x.ListForTripAsync(1))
            .ReturnsAsync(new List<AccommodationReservation>());

        var result = await accommodationService.ListAsync(1);

        Assert.Empty(result);
    }
}