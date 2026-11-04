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
        catalogService = new CatalogService(mockPlaces.Object, mockFavorites.Object, mockReviews.Object, userSession, operations);
        userSession.SignIn(new User { UserId = 1, FirstName = "Tourist", Role = "Tourist" });
    }

    protected void SignInAsPartner()
    {
        userSession.SignIn(new User { UserId = 2, FirstName = "Partner", Role = "Partner" });
    }

    protected static void SetupList<T>(Mock<IRepository<T>> mock, List<T> all)
        where T : class
    {
        mock.Setup(x => x.ListAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync((Expression<Func<T, bool>> predicate) => all.Where(predicate.Compile()).ToList());
    }

    protected static Place NewPlace(int id, string name = "Louvre", string category = "Museum", decimal price = 100, string status = "Active")
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
        var outcome1 = await catalogService.SearchAsync("", "", null, false);
        Assert.True(outcome1.IsSuccess, outcome1.Error);
        var result = outcome1.Value!;
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Search_NeverReturnsHiddenPlaces()
    {
        var outcome2 = await catalogService.SearchAsync("", "", null, false);
        Assert.True(outcome2.IsSuccess, outcome2.Error);
        var result = outcome2.Value!;
        Assert.DoesNotContain(result, p => p.Status == "Hidden");
        Assert.DoesNotContain(result, p => p.PlaceId == 4);
    }

    [Fact]
    public async Task Search_ByNameText_IgnoresCase()
    {
        var outcome3 = await catalogService.SearchAsync("LOUV", "", null, false);
        Assert.True(outcome3.IsSuccess, outcome3.Error);
        var result = outcome3.Value!;
        Assert.Single(result);
        Assert.Equal("Louvre", result[0].Name);
    }

    [Fact]
    public async Task Search_ByCategory_ReturnsOnlyMatchingCategory()
    {
        var outcome4 = await catalogService.SearchAsync("", "museum", null, false);
        Assert.True(outcome4.IsSuccess, outcome4.Error);
        var result = outcome4.Value!;
        Assert.Single(result);
        Assert.Equal(1, result[0].PlaceId);
    }

    [Fact]
    public async Task Search_ByTextAndCategory_AppliesBothFilters()
    {
        var outcome5 = await catalogService.SearchAsync("Eiffel", "Museum", null, false);
        Assert.True(outcome5.IsSuccess, outcome5.Error);
        var result = outcome5.Value!;
        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_TrimsSearchText()
    {
        var outcome6 = await catalogService.SearchAsync("  Louvre  ", "", null, false);
        Assert.True(outcome6.IsSuccess, outcome6.Error);
        var result = outcome6.Value!;
        Assert.Single(result);
    }

    [Fact]
    public async Task Search_WithMaxPrice_ReturnsPlacesNotMoreExpensive()
    {
        var outcome7 = await catalogService.SearchAsync("", "", 150, false);
        Assert.True(outcome7.IsSuccess, outcome7.Error);
        var result = outcome7.Value!;
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.True(p.Price <= 150));
        Assert.DoesNotContain(result, p => p.PlaceId == 2);
    }

    [Fact]
    public async Task Search_WithNoMatches_ReturnsEmptyList()
    {
        var outcome8 = await catalogService.SearchAsync("Nonexistent", "", null, false);
        Assert.True(outcome8.IsSuccess, outcome8.Error);
        var result = outcome8.Value!;
        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_WithTooLongText_ReturnsFailure()
    {
        var exception = await catalogService.SearchAsync(new string ('A', 201), "", null, false);
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("занадто довгий", exception.Error);
    }

    [Fact]
    public async Task Search_WithTooLongCategory_ReturnsFailure()
    {
        var exception = await catalogService.SearchAsync("", new string ('A', 101), null, false);
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("занадто довгий", exception.Error);
    }

    [Fact]
    public async Task Search_WithNegativeMaxPrice_ReturnsFailure()
    {
        var failure9 = await catalogService.SearchAsync("", "", -10, false);
        Assert.True(failure9.IsFailure, failure9.Error);
    }

    [Fact]
    public async Task Search_OnlyFavorites_ReturnsOnlyCurrentUserFavorites()
    {
        SetupList(mockFavorites, new List<Favorite> { new Favorite { UserId = 1, PlaceId = 1 }, new Favorite { UserId = 2, PlaceId = 2 } });
        var outcome10 = await catalogService.SearchAsync("", "", null, true);
        Assert.True(outcome10.IsSuccess, outcome10.Error);
        var result = outcome10.Value!;
        Assert.Single(result);
        Assert.Equal(1, result[0].PlaceId);
    }

    [Fact]
    public async Task Search_OnlyFavorites_WithoutFavorites_ReturnsEmptyList()
    {
        SetupList(mockFavorites, new List<Favorite>());
        var outcome11 = await catalogService.SearchAsync("", "", null, true);
        Assert.True(outcome11.IsSuccess, outcome11.Error);
        var result = outcome11.Value!;
        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_OnlyFavorites_AsNonTourist_ReturnsFailure()
    {
        SignInAsPartner();
        SetupList(mockFavorites, new List<Favorite>());
        var failure12 = await catalogService.SearchAsync("", "", null, true);
        Assert.True(failure12.IsFailure, failure12.Error);
        mockFavorites.Verify(x => x.ListAsync(It.IsAny<Expression<Func<Favorite, bool>>>()), Times.Never);
    }
}

public class CatalogServiceDetailsTests : CatalogServiceTestBase
{
    [Fact]
    public async Task Details_ReturnsPlaceWithReviewsAndAverageRating()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review> { new Review { PlaceId = 1, UserId = 1, Rating = 5, Comment = "A" }, new Review { PlaceId = 1, UserId = 2, Rating = 4, Comment = "B" }, new Review { PlaceId = 1, UserId = 3, Rating = 4, Comment = "C" } });
        var outcome13 = await catalogService.DetailsAsync(1);
        Assert.True(outcome13.IsSuccess, outcome13.Error);
        var result = outcome13.Value!;
        Assert.Equal(1, result.Place.PlaceId);
        Assert.Equal(3, result.Reviews.Count);
        Assert.Equal(4.33m, result.AverageRating);
    }

    [Fact]
    public async Task Details_WithoutReviews_ReturnsNullAverage()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review>());
        var outcome14 = await catalogService.DetailsAsync(1);
        Assert.True(outcome14.IsSuccess, outcome14.Error);
        var result = outcome14.Value!;
        Assert.Empty(result.Reviews);
        Assert.Null(result.AverageRating);
    }

    [Fact]
    public async Task Details_CountsOnlyReviewsOfThisPlace()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review> { new Review { PlaceId = 1, UserId = 1, Rating = 5, Comment = "Mine" }, new Review { PlaceId = 2, UserId = 1, Rating = 1, Comment = "Other place" } });
        var outcome15 = await catalogService.DetailsAsync(1);
        Assert.True(outcome15.IsSuccess, outcome15.Error);
        var result = outcome15.Value!;
        Assert.Single(result.Reviews);
        Assert.Equal(5, result.AverageRating);
    }

    [Fact]
    public async Task Details_WithNonexistentPlace_ReturnsFailure()
    {
        mockPlaces.Setup(x => x.FindAsync(404)).ReturnsAsync((Place? )null);
        var exception = await catalogService.DetailsAsync(404);
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("Місце не знайдене", exception.Error);
    }

    [Fact]
    public async Task Details_WithHiddenPlace_ReturnsFailure()
    {
        SetupPlace(NewPlace(1, status: "Hidden"));
        var exception = await catalogService.DetailsAsync(1);
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("недоступне", exception.Error);
        mockReviews.Verify(x => x.ListAsync(It.IsAny<Expression<Func<Review, bool>>>()), Times.Never);
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
        mockFavorites.Setup(x => x.AddAsync(It.IsAny<Favorite>())).Callback<Favorite>(f => added = f).Returns(Task.CompletedTask);
        var outcome16 = await catalogService.ToggleFavoriteAsync(1);
        Assert.True(outcome16.IsSuccess, outcome16.Error);
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
        SetupList(mockFavorites, new List<Favorite> { new Favorite { UserId = 1, PlaceId = 1 } });
        mockFavorites.Setup(x => x.DeleteAsync(It.IsAny<Favorite>())).Returns(Task.CompletedTask);
        var outcome17 = await catalogService.ToggleFavoriteAsync(1);
        Assert.True(outcome17.IsSuccess, outcome17.Error);
        mockFavorites.Verify(x => x.DeleteAsync(It.IsAny<Favorite>()), Times.Once);
        mockFavorites.Verify(x => x.AddAsync(It.IsAny<Favorite>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFavorite_IgnoresFavoriteOfAnotherUser()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockFavorites, new List<Favorite> { new Favorite { UserId = 2, PlaceId = 1 } });
        mockFavorites.Setup(x => x.AddAsync(It.IsAny<Favorite>())).Returns(Task.CompletedTask);
        var outcome18 = await catalogService.ToggleFavoriteAsync(1);
        Assert.True(outcome18.IsSuccess, outcome18.Error);
        mockFavorites.Verify(x => x.AddAsync(It.IsAny<Favorite>()), Times.Once);
        mockFavorites.Verify(x => x.DeleteAsync(It.IsAny<Favorite>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFavorite_WithNonexistentPlace_ReturnsFailure()
    {
        mockPlaces.Setup(x => x.FindAsync(404)).ReturnsAsync((Place? )null);
        var exception = await catalogService.ToggleFavoriteAsync(404);
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("Місце не знайдене", exception.Error);
        mockFavorites.Verify(x => x.AddAsync(It.IsAny<Favorite>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFavorite_WithHiddenPlace_ReturnsFailure()
    {
        SetupPlace(NewPlace(1, status: "Hidden"));
        var exception = await catalogService.ToggleFavoriteAsync(1);
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("недоступне", exception.Error);
        mockFavorites.Verify(x => x.AddAsync(It.IsAny<Favorite>()), Times.Never);
    }

    [Fact]
    public async Task ToggleFavorite_AsNonTourist_ReturnsFailure()
    {
        SignInAsPartner();
        SetupPlace(NewPlace(1));
        var failure19 = await catalogService.ToggleFavoriteAsync(1);
        Assert.True(failure19.IsFailure, failure19.Error);
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
        mockReviews.Setup(x => x.AddAsync(It.IsAny<Review>())).Callback<Review>(r => added = r).Returns(Task.CompletedTask);
        DateTime before = DateTime.UtcNow.AddMinutes(-1);
        var outcome20 = await catalogService.AddReviewAsync(1, 5, "Чудове місце");
        Assert.True(outcome20.IsSuccess, outcome20.Error);
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
        mockReviews.Setup(x => x.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);
        var outcome21 = await catalogService.AddReviewAsync(1, rating, "Коментар");
        Assert.True(outcome21.IsSuccess, outcome21.Error);
        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task AddReview_WithInvalidRating_ReturnsFailure(int rating)
    {
        var exception = await catalogService.AddReviewAsync(1, rating, "Коментар");
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("від 1 до 5", exception.Error);
        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_WithEmptyComment_ReturnsFailure()
    {
        var exception = await catalogService.AddReviewAsync(1, 5, "");
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("Відгук", exception.Error);
        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_WithCommentExceeding2000Chars_ReturnsFailure()
    {
        var failure22 = await catalogService.AddReviewAsync(1, 5, new string ('A', 2001));
        Assert.True(failure22.IsFailure, failure22.Error);
        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_WhenAlreadyReviewed_ReturnsFailure()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review> { new Review { PlaceId = 1, UserId = 1, Rating = 4, Comment = "Раніше" } });
        var exception = await catalogService.AddReviewAsync(1, 5, "Ще один");
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("вже залишили відгук", exception.Error);
        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_WhenOnlyAnotherUserReviewed_Succeeds()
    {
        SetupPlace(NewPlace(1));
        SetupList(mockReviews, new List<Review> { new Review { PlaceId = 1, UserId = 2, Rating = 3, Comment = "Чужий" } });
        mockReviews.Setup(x => x.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);
        var outcome23 = await catalogService.AddReviewAsync(1, 5, "Мій відгук");
        Assert.True(outcome23.IsSuccess, outcome23.Error);
        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Once);
    }

    [Fact]
    public async Task AddReview_ForNonexistentPlace_ReturnsFailure()
    {
        mockPlaces.Setup(x => x.FindAsync(404)).ReturnsAsync((Place? )null);
        var exception = await catalogService.AddReviewAsync(404, 5, "Коментар");
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("Місце не знайдене", exception.Error);
        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_ForHiddenPlace_ReturnsFailure()
    {
        SetupPlace(NewPlace(1, status: "Hidden"));
        var exception = await catalogService.AddReviewAsync(1, 5, "Коментар");
        Assert.True(exception.IsFailure, exception.Error);
        Assert.Contains("недоступне", exception.Error);
        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_AsNonTourist_ReturnsFailure()
    {
        SignInAsPartner();
        SetupPlace(NewPlace(1));
        var failure24 = await catalogService.AddReviewAsync(1, 5, "Коментар");
        Assert.True(failure24.IsFailure, failure24.Error);
        mockReviews.Verify(x => x.AddAsync(It.IsAny<Review>()), Times.Never);
    }
}
