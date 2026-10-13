using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed record PlaceDetails(
    Place Place,
    IReadOnlyList<Review> Reviews,
    decimal? AverageRating);

public sealed class CatalogService(
    IRepository<Place> places,
    IRepository<Favorite> favorites,
    IRepository<Review> reviews,
    UserSession session,
    ServiceOperations operations)
{
    public Task<IReadOnlyList<Place>> SearchAsync(
        string text,
        string category,
        decimal? maxPrice,
        bool onlyFavorites)
    {
        return operations.RunAsync(
            "SearchPlaces",
            async () =>
            {
                if (text.Length > 200 || category.Length > 100)
                {
                    throw new ArgumentException(
                        "Пошуковий текст занадто довгий.");
                }

                if (maxPrice is not null)
                {
                    ServiceOperations.Money(maxPrice.Value);
                }

                string search = text.Trim();
                string filter = category.Trim();

                IReadOnlyList<Place> candidates =
                    await places.ListAsync(x =>
                        x.Status != "Hidden"
                        && (maxPrice == null
                            || x.Price <= maxPrice))
                    .ConfigureAwait(false);

                IReadOnlyList<Place> result = candidates
                    .Where(x =>
                        x.Name.Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase)
                        && x.Category.Contains(
                            filter,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!onlyFavorites)
                {
                    return result;
                }

                int userId =
                    session.RequireRole("Tourist").UserId;

                IReadOnlyList<Favorite> saved =
                    await favorites.ListAsync(
                        x => x.UserId == userId)
                    .ConfigureAwait(false);

                var ids = saved
                    .Select(x => x.PlaceId)
                    .ToHashSet();

                return result
                    .Where(x => ids.Contains(x.PlaceId))
                    .ToList();
            });
    }

    public Task<PlaceDetails> DetailsAsync(int id)
    {
        return operations.RunAsync(
            "ViewPlaceDetails",
            async () =>
            {
                Place place = await VisibleAsync(id)
                    .ConfigureAwait(false);

                IReadOnlyList<Review> items =
                    await reviews.ListAsync(
                        x => x.PlaceId == id)
                    .ConfigureAwait(false);

                decimal? average = items.Count == 0
                    ? null
                    : decimal.Round(
                        items.Average(x => (decimal)x.Rating),
                        2);

                return new PlaceDetails(
                    place, items, average);
            });
    }

    public Task ToggleFavoriteAsync(int placeId)
    {
        return operations.RunAsync(
            "ToggleFavorite",
            async () =>
            {
                int userId =
                    session.RequireRole("Tourist").UserId;

                await VisibleAsync(placeId)
                    .ConfigureAwait(false);

                Favorite? current =
                    (await favorites.ListAsync(x =>
                        x.UserId == userId
                        && x.PlaceId == placeId)
                    .ConfigureAwait(false))
                    .SingleOrDefault();

                if (current is null)
                {
                    await favorites.AddAsync(new Favorite
                    {
                        UserId = userId,
                        PlaceId = placeId
                    }).ConfigureAwait(false);
                }
                else
                {
                    await favorites.DeleteAsync(current)
                        .ConfigureAwait(false);
                }
            });
    }

    public Task AddReviewAsync(
        int placeId,
        int rating,
        string comment)
    {
        return operations.RunAsync(
            "LeaveReview",
            async () =>
            {
                int userId =
                    session.RequireRole("Tourist").UserId;

                if (rating < 1 || rating > 5)
                {
                    throw new ArgumentException(
                        "Оцінка повинна бути від 1 до 5.");
                }

                string validComment =
                    ServiceOperations.Required(
                        comment, "Відгук", 2000);

                await VisibleAsync(placeId)
                    .ConfigureAwait(false);

                IReadOnlyList<Review> existing =
                    await reviews.ListAsync(x =>
                        x.UserId == userId
                        && x.PlaceId == placeId)
                    .ConfigureAwait(false);

                if (existing.Count != 0)
                {
                    throw new ArgumentException(
                        "Ви вже залишили відгук для цього місця.");
                }

                await reviews.AddAsync(new Review
                {
                    PlaceId = placeId,
                    UserId = userId,
                    Rating = rating,
                    Comment = validComment,
                    CreatedAt = DateTime.UtcNow
                }).ConfigureAwait(false);
            });
    }

    private async Task<Place> VisibleAsync(int id)
    {
        Place place = await places.FindAsync(id)
            .ConfigureAwait(false)
            ?? throw new ArgumentException(
                "Місце не знайдене.");

        if (place.Status == "Hidden")
        {
            throw new ArgumentException(
                "Місце недоступне.");
        }

        return place;
    }
}