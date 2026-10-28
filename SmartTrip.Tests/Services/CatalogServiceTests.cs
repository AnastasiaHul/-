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

// Спільне налаштування поточний користувач — турист з Id = 1
public abstract class CatalogServiceTestBase
{
    protected readonly Mock<IRepository<Place>> mockPlaces;
    protected readonly Mock<IRepository<Favorite>> mockFavorites;
    protected readonly Mock<IRepository<Review>> mockReviews;
    protected readonly Mock<IAppLogger> mockLogger;
    protected readonly UserSession userSession;
    protected readonly ServiceOperations operations;
    protected readonly CatalogService catalogService;

    protected CatalogServiceTestBase()
    {
        mockPlaces = new Mock<IRepository<Place>>();
        mockFavorites = new Mock<IRepository<Favorite>>();
        mockReviews = new Mock<IRepository<Review>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        catalogService = new CatalogService(
            mockPlaces.Object,
            mockFavorites.Object,
            mockReviews.Object,
            userSession,
            operations);

        userSession.SignIn(new User { UserId = 1, FirstName = "Tourist", Role = "Tourist" });
    }

    protected void SignInAsPartner()
    {
        userSession.SignIn(new User { UserId = 2, FirstName = "Partner", Role = "Partner" });
    }

   
    protected static void SetupList<T>(Mock<IRepository<T>> mock, List<T> all)
        where T : class
    {
        mock
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<T, bool>>>()))
            .ReturnsAsync((Expression<Func<T, bool>> predicate) =>
                all.Where(predicate.Compile()).ToList());
    }

    protected static Place NewPlace(
        int id,
        string name = "Louvre",
        string category = "Museum",
        decimal price = 100,
        string status = "Active")
    {
        return new Place
        {
            PlaceId = id,
            Name = name,
            Category = category,
            Price = price,
            Status = status
        };
    }

    protected void SetupPlace(Place place)
    {
        mockPlaces.Setup(x => x.FindAsync(place.PlaceId)).ReturnsAsync(place);
    }
}

public class CatalogServiceSearchTests : CatalogServiceTestBase
{
    private readonly List<Place> allPlaces;

    public CatalogServiceSearchTests()
    {
        allPlaces = new List<Place>
        {
            NewPlace(1, "Louvre", "Museum", 150),
            NewPlace(2, "Eiffel Tower", "Landmark", 300),
            NewPlace(3, "City Park", "Park", 0),
            NewPlace(4, "Secret Place", "Museum", 50, "Hidden")
        };

        SetupList(mockPlaces, allPlaces);
    }

    [Fact]
    public async Task Search_WithEmptyFilters_ReturnsAllVisiblePlaces()
    {
        var result = await catalogService.SearchAsync("", "", null, false);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Search_NeverReturnsHiddenPlaces()
    {
        var result = await catalogService.SearchAsync("", "", null, false);

        Assert.DoesNotContain(result, p => p.Status == "Hidden");
        Assert.DoesNotContain(result, p => p.PlaceId == 4);
    }

    [Fact]
    public async Task Search_ByNameText_IgnoresCase()
    {
        var result = await catalogService.SearchAsync("LOUV", "", null, false);

        Assert.Single(result);
        Assert.Equal("Louvre", result[0].Name);
    }

    [Fact]
    public async Task Search_ByCategory_ReturnsOnlyMatchingCategory()
    {
        var result = await catalogService.SearchAsync("", "museum", null, false);

        Assert.Single(result);
        Assert.Equal(1, result[0].PlaceId);
    }

    [Fact]
    public async Task Search_ByTextAndCategory_AppliesBothFilters()
    {
        var result = await catalogService.SearchAsync("Eiffel", "Museum", null, false);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_TrimsSearchText()
    {
        var result = await catalogService.SearchAsync("  Louvre  ", "", null, false);

        Assert.Single(result);
    }

    [Fact]
    public async Task Search_WithMaxPrice_ReturnsPlacesNotMoreExpensive()
    {
        var result = await catalogService.SearchAsync("", "", 150, false);

        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.True(p.Price <= 150));
        Assert.DoesNotContain(result, p => p.PlaceId == 2);
    }

    [Fact]
    public async Task Search_WithNoMatches_ReturnsEmptyList()
    {
        var result = await catalogService.SearchAsync("Nonexistent", "", null, false);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_WithTooLongText_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.SearchAsync(new string('A', 201), "", null, false));

        Assert.Contains("занадто довгий", exception.Message);
    }

    [Fact]
    public async Task Search_WithTooLongCategory_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.SearchAsync("", new string('A', 101), null, false));

        Assert.Contains("занадто довгий", exception.Message);
    }

    [Fact]
    public async Task Search_WithNegativeMaxPrice_ThrowsException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.SearchAsync("", "", -10, false));
    }

    [Fact]
    public async Task Search_OnlyFavorites_ReturnsOnlyCurrentUserFavorites()
    {
        SetupList(mockFavorites, new List<Favorite>
        {
            new Favorite { UserId = 1, PlaceId = 1 },
            new Favorite { UserId = 2, PlaceId = 2 }
        });

        var result = await catalogService.SearchAsync("", "", null, true);

        Assert.Single(result);
        Assert.Equal(1, result[0].PlaceId);
    }

    [Fact]
    public async Task Search_OnlyFavorites_WithoutFavorites_ReturnsEmptyList()
    {
        SetupList(mockFavorites, new List<Favorite>());

        var result = await catalogService.SearchAsync("", "", null, true);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_OnlyFavorites_AsNonTourist_ThrowsException()
    {
        SignInAsPartner();
        SetupList(mockFavorites, new List<Favorite>());

        await Assert.ThrowsAnyAsync<Exception>(
            () => catalogService.SearchAsync("", "", null, true));

        mockFavorites.Verify(
            x => x.ListAsync(It.IsAny<Expression<Func<Favorite, bool>>>()),
            Times.Never);
    }
}

public class CatalogServiceDetailsTests : CatalogServiceTestBase
{
    [Fact]
    public async Task Details_ReturnsPlaceWithReviewsAndAverageRating()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review>
        {
            new Review { PlaceId = 1, UserId = 1, Rating = 5, Comment = "A" },
            new Review { PlaceId = 1, UserId = 2, Rating = 4, Comment = "B" },
            new Review { PlaceId = 1, UserId = 3, Rating = 4, Comment = "C" }
        });

        var result = await catalogService.DetailsAsync(1);

        Assert.Equal(1, result.Place.PlaceId);
        Assert.Equal(3, result.Reviews.Count);
        Assert.Equal(4.33m, result.AverageRating);
    }

    [Fact]
    public async Task Details_WithoutReviews_ReturnsNullAverage()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review>());

        var result = await catalogService.DetailsAsync(1);

        Assert.Empty(result.Reviews);
        Assert.Null(result.AverageRating);
    }

    [Fact]
    public async Task Details_CountsOnlyReviewsOfThisPlace()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review>
        {
            new Review { PlaceId = 1, UserId = 1, Rating = 5, Comment = "Mine" },
            new Review { PlaceId = 2, UserId = 1, Rating = 1, Comment = "Other place" }
        });

        var result = await catalogService.DetailsAsync(1);

        Assert.Single(result.Reviews);
        Assert.Equal(5, result.AverageRating);
    }

    [Fact]
    public async Task Details_WithNonexistentPlace_ThrowsException()
    {
        mockPlaces.Setup(x => x.FindAsync(404)).ReturnsAsync((Place?)null);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.DetailsAsync(404));

        Assert.Contains("Місце не знайдене", exception.Message);
    }

    [Fact]
    public async Task Details_WithHiddenPlace_ThrowsException()
    {
        SetupPlace(NewPlace(1, status: "Hidden"));

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.DetailsAsync(1));

        Assert.Contains("недоступне", exception.Message);

        mockReviews.Verify(
            x => x.ListAsync(It.IsAny<Expression<Func<Review, bool>>>()),
            Times.Never);
    }
}

public class CatalogServiceToggleFavoriteTests : CatalogServiceTestBase
{
    [Fact]
    public async Task ToggleFavorite_WhenNotFavorite_AddsFavorite()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockFavorites, new List<Favorite>());

        Favorite? added = null;
        mockFavorites
            .Setup(x => x.AddAsync(It.IsAny<Favorite>()))
            .Callback<Favorite>(f => added = f)
            .Returns(Task.CompletedTask);

        await catalogService.ToggleFavoriteAsync(1);

        Assert.NotNull(added);
        Assert.Equal(1, added!.UserId);
        Assert.Equal(1, added.PlaceId);

        mockFavorites.Verify(x => x.AddAsync(It.IsAny<Favorite>()), Times.Once);
        mockFavorites.Verify(x => x.DeleteAsync(It.IsAny<Favorite>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFavorite_WhenAlreadyFavorite_RemovesFavorite()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockFavorites, new List<Favorite>
        {
            new Favorite { UserId = 1, PlaceId = 1 }
        });
        mockFavorites
            .Setup(x => x.DeleteAsync(It.IsAny<Favorite>()))
            .Returns(Task.CompletedTask);

        await catalogService.ToggleFavoriteAsync(1);

        mockFavorites.Verify(x => x.DeleteAsync(It.IsAny<Favorite>()), Times.Once);
        mockFavorites.Verify(x => x.AddAsync(It.IsAny<Favorite>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFavorite_IgnoresFavoriteOfAnotherUser()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockFavorites, new List<Favorite>
        {
            new Favorite { UserId = 2, PlaceId = 1 }
        });
        mockFavorites
            .Setup(x => x.AddAsync(It.IsAny<Favorite>()))
            .Returns(Task.CompletedTask);

        await catalogService.ToggleFavoriteAsync(1);

        mockFavorites.Verify(x => x.AddAsync(It.IsAny<Favorite>()), Times.Once);
        mockFavorites.Verify(x => x.DeleteAsync(It.IsAny<Favorite>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFavorite_WithNonexistentPlace_ThrowsException()
    {
        mockPlaces.Setup(x => x.FindAsync(404)).ReturnsAsync((Place?)null);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.ToggleFavoriteAsync(404));

        Assert.Contains("Місце не знайдене", exception.Message);

        mockFavorites.Verify(x => x.AddAsync(It.IsAny<Favorite>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFavorite_WithHiddenPlace_ThrowsException()
    {
        SetupPlace(NewPlace(1, status: "Hidden"));

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.ToggleFavoriteAsync(1));

        Assert.Contains("недоступне", exception.Message);

        mockFavorites.Verify(x => x.AddAsync(It.IsAny<Favorite>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFavorite_AsNonTourist_ThrowsException()
    {
        SignInAsPartner();
        SetupPlace(NewPlace(1));

        await Assert.ThrowsAnyAsync<Exception>(
            () => catalogService.ToggleFavoriteAsync(1));

        mockFavorites.Verify(x => x.AddAsync(It.IsAny<Favorite>()), Times.Never);
        mockFavorites.Verify(x => x.DeleteAsync(It.IsAny<Favorite>()), Times.Never);
    }
}

public class CatalogServiceAddReviewTests : CatalogServiceTestBase
{
    [Fact]
    public async Task AddReview_WithValidData_SavesReview()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review>());

        Review? added = null;
        mockReviews
            .Setup(x => x.AddAsync(It.IsAny<Review>()))
            .Callback<Review>(r => added = r)
            .Returns(Task.CompletedTask);

        DateTime before = DateTime.UtcNow.AddMinutes(-1);

        await catalogService.AddReviewAsync(1, 5, "Чудове місце");

        Assert.NotNull(added);
        Assert.Equal(1, added!.PlaceId);
        Assert.Equal(1, added.UserId);
        Assert.Equal(5, added.Rating);
        Assert.Equal("Чудове місце", added.Comment);
        Assert.True(added.CreatedAt >= before);

        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task AddReview_WithBoundaryRating_Succeeds(int rating)
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review>());
        mockReviews
            .Setup(x => x.AddAsync(It.IsAny<Review>()))
            .Returns(Task.CompletedTask);

        await catalogService.AddReviewAsync(1, rating, "Коментар");

        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task AddReview_WithInvalidRating_ThrowsException(int rating)
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.AddReviewAsync(1, rating, "Коментар"));

        Assert.Contains("від 1 до 5", exception.Message);

        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_WithEmptyComment_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.AddReviewAsync(1, 5, ""));

        Assert.Contains("Відгук", exception.Message);

        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_WithCommentExceeding2000Chars_ThrowsException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.AddReviewAsync(1, 5, new string('A', 2001)));

        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_WhenAlreadyReviewed_ThrowsException()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review>
        {
            new Review { PlaceId = 1, UserId = 1, Rating = 4, Comment = "Раніше" }
        });

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.AddReviewAsync(1, 5, "Ще один"));

        Assert.Contains("вже залишили відгук", exception.Message);

        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_WhenOnlyAnotherUserReviewed_Succeeds()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review>
        {
            new Review { PlaceId = 1, UserId = 2, Rating = 3, Comment = "Чужий" }
        });
        mockReviews
            .Setup(x => x.AddAsync(It.IsAny<Review>()))
            .Returns(Task.CompletedTask);

        await catalogService.AddReviewAsync(1, 5, "Мій відгук");

        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Once);
    }

    [Fact]
    public async Task AddReview_ForNonexistentPlace_ThrowsException()
    {
        mockPlaces.Setup(x => x.FindAsync(404)).ReturnsAsync((Place?)null);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.AddReviewAsync(404, 5, "Коментар"));

        Assert.Contains("Місце не знайдене", exception.Message);

        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_ForHiddenPlace_ThrowsException()
    {
        SetupPlace(NewPlace(1, status: "Hidden"));

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => catalogService.AddReviewAsync(1, 5, "Коментар"));

        Assert.Contains("недоступне", exception.Message);

        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_AsNonTourist_ThrowsException()
    {
        SignInAsPartner();
        SetupPlace(NewPlace(1));

        await Assert.ThrowsAnyAsync<Exception>(
            () => catalogService.AddReviewAsync(1, 5, "Коментар"));

        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }
}