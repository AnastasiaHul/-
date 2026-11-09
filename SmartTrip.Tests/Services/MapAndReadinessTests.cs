using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Serilog;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Logging;
using Xunit;

namespace SmartTrip.Tests.Services;

public class MapAndReadinessTests
{
    private static readonly DateTime Start = new(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc);
    private readonly Mock<IAppLogger> log = new();
    private readonly Mock<IRepository<TripPhoto>> photos = new();
    private readonly Mock<IRepository<Place>> places = new();
    private readonly Mock<IAccommodationReservationRepository> housing = new();
    private readonly UserSession session = new();
    private readonly TripService trips;
    private readonly TravelerMapService map;

    public MapAndReadinessTests()
    {
        session.SignIn(new User { UserId = 7, Role = "Tourist" });
        var tripRecords = new Mock<IRepository<Trip>>();
        tripRecords.Setup(x => x.FindAsync(1)).ReturnsAsync(
            new Trip { TripId = 1, UserId = 7, StartDate = Start, EndDate = Start.AddDays(3) });
        trips = new TripService(tripRecords.Object, session, new ServiceOperations(log.Object, session));
        places.Setup(x => x.FindAsync(2)).ReturnsAsync(
            new Place { PlaceId = 2, Name = "Музей", Address = "Львів, площа Ринок, 1" });
        photos.Setup(x => x.FindAsync(5)).ReturnsAsync(
            new TripPhoto { TripPhotoId = 5, TripId = 1, Address = "Львів, площа Ринок, 1" });
        photos.Setup(x => x.FindAsync(6)).ReturnsAsync(
            new TripPhoto { TripPhotoId = 6, TripId = 1, Address = "Львів, проспект Свободи, 28" });
        photos.Setup(x => x.FindAsync(7)).ReturnsAsync(
            new TripPhoto { TripPhotoId = 7, TripId = 1, Address = "Львів, площа Ринок, 1" });
        map = new TravelerMapService(photos.Object, places.Object, trips, session, log.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddPhoto_StoresOnlyValidInputAndLogsOutcome(bool valid)
    {
        byte[] bytes = valid ? Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jJZkAAAAASUVORK5CYII=")
            : [1, 2, 3];
        var result = await map.AddPhotoAsync(1, 2, "Біля музею", bytes);

        Assert.Equal(valid, result.IsSuccess);
        photos.Verify(x => x.AddAsync(It.Is<TripPhoto>(p =>
            p.TripId == 1 && p.PlaceId == 2 && p.Image.Length == bytes.Length)),
            valid ? Times.Once() : Times.Never());
        VerifyOutcome("Map.AddPhoto", valid);
    }

    [Theory]
    [InlineData("Кав’ярні", true)]
    [InlineData("", false)]
    public async Task FindNearby_UsesAddressAndCategoryAndLogsOutcome(string category, bool valid)
    {
        var result = await map.FindNearbyAsync(2, category);

        Assert.Equal(valid, result.IsSuccess);
        if (valid)
        {
            Assert.Contains("Кав’ярні near Львів", Uri.UnescapeDataString(result.Value!));
        }

        VerifyOutcome("Map.FindNearby", valid);
    }

    [Theory]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public async Task BuildRoute_RequiresDifferentLocationsAndLogsOutcome(int secondId, bool valid)
    {
        var result = await map.BuildPhotoRouteAsync(1, new[] { 5, secondId });

        Assert.Equal(valid, result.IsSuccess);
        if (valid)
        {
            Assert.Contains("destination=Львів, проспект Свободи, 28", Uri.UnescapeDataString(result.Value!));
        }

        VerifyOutcome("Map.BuildPhotoRoute", valid);
    }

    [Fact]
    public async Task Readiness_CountsDistinctCoveredNightsAndIgnoresOptionalBookings()
    {
        housing.Setup(x => x.ListForTripAsync(1)).ReturnsAsync(new[]
        {
            new AccommodationReservation { CheckIn = Start, CheckOut = Start.AddDays(2) },
            new AccommodationReservation { CheckIn = Start, CheckOut = Start.AddDays(1) },
            new AccommodationReservation { CheckIn = Start.AddDays(2), CheckOut = Start.AddDays(3), Status = "Cancelled" }
        });

        var result = await MakeReadiness(log.Object).CheckAsync(1, new BookingNeeds(false, true, false, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.RequiredCount);
        Assert.Equal(0, result.Value.ReadyCount);
        Assert.Contains("2 із 3", result.Value.Items.Single(x => x.Required).Details);
        VerifyOutcome("Booking.ReadinessCheck", true);
    }

    [Fact]
    public async Task Readiness_RejectsEmptySelectionAndWritesLogFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "smarttrip-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "readiness.log");
        try
        {
            using (var serilog = new LoggerConfiguration().WriteTo.File(path).CreateLogger())
            {
                var result = await MakeReadiness(new SerilogAppLogger(serilog))
                    .CheckAsync(1, new BookingNeeds(false, false, false, false));
                Assert.True(result.IsFailure);
            }

            string written = File.ReadAllText(path);
            Assert.Contains("Booking.ReadinessCheck.ValidationFailed", written);
            Assert.Contains("Позначте хоча б один", written);
            Assert.DoesNotContain("succeeded", written);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private BookingReadinessService MakeReadiness(IAppLogger logger) =>
        new(trips, session, logger, new Mock<ITrainReservationRepository>().Object,
            new Mock<IRepository<TrainDeparture>>().Object, housing.Object,
            new Mock<ICarReservationRepository>().Object, new Mock<IExcursionReservationRepository>().Object,
            new Mock<IRepository<ExcursionSession>>().Object);

    private void VerifyOutcome(string action, bool success)
    {
        string prefix = action + (success ? ".Succeeded:" : ".ValidationFailed:");
        log.Verify(x => x.Information(It.Is<string>(msg =>
            msg.StartsWith(prefix, StringComparison.Ordinal)), 7), Times.Once);
    }
}
