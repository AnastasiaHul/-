using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Tests.Services;

public abstract class TravelerMapBalancedTestBase
{
    protected readonly Mock<IRepository<TripPhoto>> mockPhotos;
    protected readonly Mock<IRepository<Place>> mockPlaces;
    protected readonly Mock<IRepository<Trip>> mockTripsRepo;
    protected readonly TripService tripService;
    protected readonly Mock<IAppLogger> mockLogger;
    protected readonly UserSession userSession;
    protected readonly TravelerMapService travelerMapService;

    protected static readonly byte[] ValidJpegBytes = [255, 216, 255, 224, 0, 16, 74, 70, 70, 70];
    protected static readonly byte[] ValidPngBytes = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0];

    protected TravelerMapBalancedTestBase()
    {
        mockPhotos = new Mock<IRepository<TripPhoto>>();
        mockPlaces = new Mock<IRepository<Place>>();
        mockTripsRepo = new Mock<IRepository<Trip>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();

        var operations = new ServiceOperations(mockLogger.Object, userSession);
        tripService = new TripService(mockTripsRepo.Object, userSession, operations);

        travelerMapService = new TravelerMapService(
            mockPhotos.Object,
            mockPlaces.Object,
            tripService,
            userSession,
            mockLogger.Object
        );

        userSession.SignIn(new User { UserId = 1, FirstName = "Марта", Role = "Tourist" });
    }

    protected void SetupOwnedTrip(int tripId = 1)
    {
        var trip = new Trip
        {
            TripId = tripId,
            UserId = 1,
            Title = "Подорож Львовом",
            City = "Львів",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(5)
        };
        mockTripsRepo.Setup(x => x.FindAsync(tripId)).ReturnsAsync(trip);
    }
}

// ==========================================
// 1. ЮЗКЕЙС: ДОДАВАННЯ ФОТО (AddPhotoAsync)
// ==========================================
public class TravelerMapAddPhotoTests : TravelerMapBalancedTestBase
{
    // Позитивні (4)
    [Fact]
    public async Task AddPhoto_ValidJpeg_Succeeds()
    {
        SetupOwnedTrip(1);
        var place = new Place { PlaceId = 10, Name = "Опера", Address = "просп. Свободи, 28", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(10)).ReturnsAsync(place);

        var result = await travelerMapService.AddPhotoAsync(1, 10, "Красивий вечір", ValidJpegBytes);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task AddPhoto_ValidPng_Succeeds()
    {
        SetupOwnedTrip(1);
        var place = new Place { PlaceId = 11, Name = "Ринок", Address = "пл. Ринок, 1", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(11)).ReturnsAsync(place);

        var result = await travelerMapService.AddPhotoAsync(1, 11, "Ратуша", ValidPngBytes);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AddPhoto_MinimalLengthCaption_Succeeds()
    {
        SetupOwnedTrip(1);
        var place = new Place { PlaceId = 10, Name = "Опера", Address = "просп. Свободи, 28", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(10)).ReturnsAsync(place);

        var result = await travelerMapService.AddPhotoAsync(1, 10, "А", ValidJpegBytes);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AddPhoto_MaxAllowedCaptionLength_Succeeds()
    {
        SetupOwnedTrip(1);
        var place = new Place { PlaceId = 10, Name = "Опера", Address = "просп. Свободи, 28", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(10)).ReturnsAsync(place);
        var maxCaption = new string('X', 120);

        var result = await travelerMapService.AddPhotoAsync(1, 10, maxCaption, ValidJpegBytes);

        Assert.True(result.IsSuccess);
    }

    // Негативні (2)
    [Fact]
    public async Task AddPhoto_InvalidImageHeader_ReturnsFailure()
    {
        SetupOwnedTrip(1);
        var place = new Place { PlaceId = 10, Name = "Опера", Address = "просп. Свободи, 28", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(10)).ReturnsAsync(place);
        var invalidBytes = new byte[] { 1, 2, 3, 4, 5 };

        var result = await travelerMapService.AddPhotoAsync(1, 10, "Підпис", invalidBytes);

        Assert.True(result.IsFailure);
        Assert.Equal("Виберіть JPG або PNG розміром до 3 МБ.", result.Error);
    }

    [Fact]
    public async Task AddPhoto_PlaceWithoutAddress_ReturnsFailure()
    {
        SetupOwnedTrip(1);
        var badPlace = new Place { PlaceId = 10, Name = "Місце", Address = "", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(10)).ReturnsAsync(badPlace);

        var result = await travelerMapService.AddPhotoAsync(1, 10, "Підпис", ValidJpegBytes);

        Assert.True(result.IsFailure);
        Assert.Equal("Виберіть доступне місце з повною адресою.", result.Error);
    }
}


// ==========================================
// 2. ЮЗКЕЙС: ПОШУК МІСЦЬ ПОРУЧ (FindNearbyAsync)
// ==========================================
public class TravelerMapFindNearbyTests : TravelerMapBalancedTestBase
{
    // Позитивні (4)
    [Fact]
    public async Task FindNearby_ValidPlaceAndCategory_ReturnsUrl()
    {
        var place = new Place { PlaceId = 5, Name = "Площа Ринок", Address = "пл. Ринок, Львів", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(5)).ReturnsAsync(place);

        var result = await travelerMapService.FindNearbyAsync(5, "Кав'ярні");

        Assert.True(result.IsSuccess);
        Assert.Contains("maps/search/?api=1&query=", result.Value);
    }

    [Fact]
    public async Task FindNearby_CategoryWithSpaces_TrimsAndSucceeds()
    {
        var place = new Place { PlaceId = 5, Name = "Площа Ринок", Address = "пл. Ринок, Львів", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(5)).ReturnsAsync(place);

        var result = await travelerMapService.FindNearbyAsync(5, "  Ресторани  ");

        Assert.True(result.IsSuccess);
        Assert.Contains("Ресторани", result.Value);
    }

    [Fact]
    public async Task FindNearby_DifferentCategory_Succeeds()
    {
        var place = new Place { PlaceId = 5, Name = "Площа Ринок", Address = "пл. Ринок, Львів", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(5)).ReturnsAsync(place);

        var result = await travelerMapService.FindNearbyAsync(5, "Музеї");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task FindNearby_EnglishCategory_Succeeds()
    {
        var place = new Place { PlaceId = 5, Name = "Площа Ринок", Address = "пл. Ринок, Львів", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(5)).ReturnsAsync(place);

        var result = await travelerMapService.FindNearbyAsync(5, "Hotels");

        Assert.True(result.IsSuccess);
    }

    // Негативні (2)
    [Fact]
    public async Task FindNearby_WhenUserNotTourist_ReturnsFailure()
    {
        userSession.SignIn(new User { UserId = 2, Role = "Partner" });
        var place = new Place { PlaceId = 5, Name = "Місце", Address = "Адреса", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(5)).ReturnsAsync(place);

        var result = await travelerMapService.FindNearbyAsync(5, "Кав'ярні");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task FindNearby_EmptyCategory_ReturnsFailure()
    {
        var place = new Place { PlaceId = 5, Name = "Місце", Address = "Адреса", Status = "Published" };
        mockPlaces.Setup(x => x.FindAsync(5)).ReturnsAsync(place);

        var result = await travelerMapService.FindNearbyAsync(5, "");

        Assert.True(result.IsFailure);
    }
}


// ==========================================
// 3. ЮЗКЕЙС: МАРШРУТ ЗА ФОТО (BuildPhotoRouteAsync)
// ==========================================
public class TravelerMapBuildRouteTests : TravelerMapBalancedTestBase
{
    // Позитивні (4)
    [Fact]
    public async Task BuildPhotoRoute_TwoPhotosDifferentAddresses_Succeeds()
    {
        SetupOwnedTrip(1);
        mockPhotos.Setup(x => x.FindAsync(1)).ReturnsAsync(new TripPhoto { TripPhotoId = 1, TripId = 1, Address = "вул. Городоцька, 1" });
        mockPhotos.Setup(x => x.FindAsync(2)).ReturnsAsync(new TripPhoto { TripPhotoId = 2, TripId = 1, Address = "проспект Свободи, 28" });

        var result = await travelerMapService.BuildPhotoRouteAsync(1, [1, 2]);

        Assert.True(result.IsSuccess);
        Assert.Contains("google.com/maps/dir/?api=1", result.Value);
    }

    [Fact]
    public async Task BuildPhotoRoute_ThreePhotosWithWaypoint_Succeeds()
    {
        SetupOwnedTrip(1);
        mockPhotos.Setup(x => x.FindAsync(1)).ReturnsAsync(new TripPhoto { TripPhotoId = 1, TripId = 1, Address = "Точка А" });
        mockPhotos.Setup(x => x.FindAsync(2)).ReturnsAsync(new TripPhoto { TripPhotoId = 2, TripId = 1, Address = "Точка Б" });
        mockPhotos.Setup(x => x.FindAsync(3)).ReturnsAsync(new TripPhoto { TripPhotoId = 3, TripId = 1, Address = "Точка В" });

        var result = await travelerMapService.BuildPhotoRouteAsync(1, [1, 2, 3]);

        Assert.True(result.IsSuccess);
        Assert.Contains("waypoints=", result.Value);
    }

    [Fact]
    public async Task BuildPhotoRoute_FourPhotos_Succeeds()
    {
        SetupOwnedTrip(1);
        mockPhotos.Setup(x => x.FindAsync(1)).ReturnsAsync(new TripPhoto { TripPhotoId = 1, TripId = 1, Address = "Адреса 1" });
        mockPhotos.Setup(x => x.FindAsync(2)).ReturnsAsync(new TripPhoto { TripPhotoId = 2, TripId = 1, Address = "Адреса 2" });
        mockPhotos.Setup(x => x.FindAsync(3)).ReturnsAsync(new TripPhoto { TripPhotoId = 3, TripId = 1, Address = "Адреса 3" });
        mockPhotos.Setup(x => x.FindAsync(4)).ReturnsAsync(new TripPhoto { TripPhotoId = 4, TripId = 1, Address = "Адреса 4" });

        var result = await travelerMapService.BuildPhotoRouteAsync(1, [1, 2, 3, 4]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task BuildPhotoRoute_FivePhotosMaximum_Succeeds()
    {
        SetupOwnedTrip(1);
        for (int i = 1; i <= 5; i++)
        {
            mockPhotos.Setup(x => x.FindAsync(i)).ReturnsAsync(new TripPhoto { TripPhotoId = i, TripId = 1, Address = $"Адреса {i}" });
        }

        var result = await travelerMapService.BuildPhotoRouteAsync(1, [1, 2, 3, 4, 5]);

        Assert.True(result.IsSuccess);
    }

    // Негативні (2)
    [Fact]
    public async Task BuildPhotoRoute_LessThanTwoPhotos_ReturnsFailure()
    {
        SetupOwnedTrip(1);

        var result = await travelerMapService.BuildPhotoRouteAsync(1, [1]);

        Assert.True(result.IsFailure);
        Assert.Equal("Виберіть від 2 до 5 фото для маршруту.", result.Error);
    }

    [Fact]
    public async Task BuildPhotoRoute_IdenticalAddresses_ReturnsFailure()
    {
        SetupOwnedTrip(1);
        mockPhotos.Setup(x => x.FindAsync(1)).ReturnsAsync(new TripPhoto { TripPhotoId = 1, TripId = 1, Address = "Однакова адреса" });
        mockPhotos.Setup(x => x.FindAsync(2)).ReturnsAsync(new TripPhoto { TripPhotoId = 2, TripId = 1, Address = "Однакова адреса" });

        var result = await travelerMapService.BuildPhotoRouteAsync(1, [1, 2]);

        Assert.True(result.IsFailure);
        Assert.Equal("Виберіть фото з двох різних місць.", result.Error);
    }
}


// ==========================================
// 4. ЮЗКЕЙС: ОТРИМАННЯ СПИСКІВ (ListPlacesAsync / ListPhotosAsync)
// ==========================================
public class TravelerMapListsTests : TravelerMapBalancedTestBase
{
    // Позитивні (4)
    [Fact]
    public async Task ListPlaces_WhenUserIsTourist_ReturnsValidPublishedPlaces()
    {
        var placesList = new List<Place>
        {
            new() { PlaceId = 1, Name = "Місце А", Address = "Адреса 1", Status = "Published" },
            new() { PlaceId = 2, Name = "Місце Б", Address = "Адреса 2", Status = "Published" }
        };
        mockPlaces.Setup(x => x.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Place, bool>>>()))
            .ReturnsAsync(placesList);

        var result = await travelerMapService.ListPlacesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task ListPlaces_OrdersAlphabeticallyByName()
    {
        var placesList = new List<Place>
        {
            new() { PlaceId = 1, Name = "Зелене", Address = "Адреса", Status = "Published" },
            new() { PlaceId = 2, Name = "Аптека", Address = "Адреса", Status = "Published" }
        };
        mockPlaces.Setup(x => x.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Place, bool>>>()))
            .ReturnsAsync(placesList);

        var result = await travelerMapService.ListPlacesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Аптека", result.Value[0].Name);
    }

    [Fact]
    public async Task ListPhotos_ValidTrip_ReturnsOrderedPhotos()
    {
        SetupOwnedTrip(1);
        var photosList = new List<TripPhoto>
        {
            new() { TripPhotoId = 2, TripId = 1, CreatedAt = DateTime.UtcNow.AddMinutes(1) },
            new() { TripPhotoId = 1, TripId = 1, CreatedAt = DateTime.UtcNow }
        };
        mockPhotos.Setup(x => x.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TripPhoto, bool>>>()))
            .ReturnsAsync(photosList);

        var result = await travelerMapService.ListPhotosAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task ListPhotos_EmptyList_ReturnsEmptyResult()
    {
        SetupOwnedTrip(1);
        mockPhotos.Setup(x => x.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Place, bool>>>()))
            .ReturnsAsync(new List<Place>()); // заміна стабу якщо потрібно
        mockPhotos.Setup(x => x.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TripPhoto, bool>>>()))
            .ReturnsAsync(new List<TripPhoto>());

        var result = await travelerMapService.ListPhotosAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    // Негативні (2)
    [Fact]
    public async Task ListPlaces_WhenUserIsNotTourist_ReturnsFailure()
    {
        userSession.SignIn(new User { UserId = 2, Role = "Partner" });

        var result = await travelerMapService.ListPlacesAsync();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ListPhotos_WhenTripNotOwned_ReturnsFailure()
    {
        mockTripsRepo.Setup(x => x.FindAsync(99)).ReturnsAsync((Trip?)null);

        var result = await travelerMapService.ListPhotosAsync(99);

        Assert.True(result.IsFailure);
    }
}