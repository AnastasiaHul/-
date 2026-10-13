using Xunit;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Tests.Entities;

public class FavoriteTests
{
    [Fact]
    public void Constructor_ShouldCreateFavoriteWithDefaultValues()
    {
        var favorite = new Favorite();

        Assert.Equal(0, favorite.FavoriteId);
        Assert.Equal(0, favorite.UserId);
        Assert.Equal(0, favorite.PlaceId);
    }

    [Theory]
    [InlineData(1, 100, 50)]
    [InlineData(5, 999, 555)]
    [InlineData(10, 1000, 200)]
    public void Properties_ShouldSetAndGetFavoriteProperties(
        int favoriteId,
        int userId,
        int placeId)
    {
        var favorite = new Favorite
        {
            FavoriteId = favoriteId,
            UserId = userId,
            PlaceId = placeId
        };

        Assert.Equal(favoriteId, favorite.FavoriteId);
        Assert.Equal(userId, favorite.UserId);
        Assert.Equal(placeId, favorite.PlaceId);
    }

    [Fact]
    public void Properties_ShouldHandleZeroValues()
    {
        var favorite = new Favorite
        {
            FavoriteId = 0,
            UserId = 0,
            PlaceId = 0
        };

        Assert.Equal(0, favorite.FavoriteId);
        Assert.Equal(0, favorite.UserId);
        Assert.Equal(0, favorite.PlaceId);
    }

    [Fact]
    public void Properties_ShouldHandleMaxValues()
    {
        var favorite = new Favorite
        {
            FavoriteId = int.MaxValue,
            UserId = int.MaxValue,
            PlaceId = int.MaxValue
        };

        Assert.Equal(int.MaxValue, favorite.FavoriteId);
        Assert.Equal(int.MaxValue, favorite.UserId);
        Assert.Equal(int.MaxValue, favorite.PlaceId);
    }
}