using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace SmartTrip.Application.Services;
public sealed record PlaceDetails(Place Place, IReadOnlyList<Review> Reviews, decimal? AverageRating);
public sealed class CatalogService(IRepository<Place> places, IRepository<Favorite> favorites, IRepository<Review> reviews, UserSession session, ServiceOperations operations)
{
    public Task<Result<IReadOnlyList<Place>>> SearchAsync(string text, string category, decimal? maxPrice, bool onlyFavorites)
    {
        return operations.RunResultAsync<IReadOnlyList<Place>>("SearchPlaces", async () =>
        {
            if (text.Length > 200 || category.Length > 100)
            {
                return new Failure("Пошуковий текст занадто довгий.");
            }

            if (maxPrice is not null)
            {
                var check1 = ServiceOperations.TryMoney(maxPrice.Value);
                if (check1.IsFailure)
                {
                    return new Failure(check1.Error);
                }
            }

            string search = text.Trim();
            string filter = category.Trim();
            IReadOnlyList<Place> candidates = await places.ListAsync(x => x.Status != "Hidden" && (maxPrice == null || x.Price <= maxPrice)).ConfigureAwait(false);
            IReadOnlyList<Place> result = candidates.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) && x.Category.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!onlyFavorites)
            {
                return Result<IReadOnlyList<Place>>.Success(result);
            }

            var check2 = session.TryRequireRole("Tourist");
            if (check2.IsFailure)
            {
                return new Failure(check2.Error);
            }

            int userId = check2.Value!.UserId;
            IReadOnlyList<Favorite> saved = await favorites.ListAsync(x => x.UserId == userId).ConfigureAwait(false);
            var ids = saved.Select(x => x.PlaceId).ToHashSet();
            return Result<IReadOnlyList<Place>>.Success(result.Where(x => ids.Contains(x.PlaceId)).ToList());
        });
    }

    public Task<Result<PlaceDetails>> DetailsAsync(int id)
    {
        return operations.RunResultAsync<PlaceDetails>("ViewPlaceDetails", async () =>
        {
            var check3 = await VisibleAsync(id).ConfigureAwait(false);
            if (check3.IsFailure)
            {
                return new Failure(check3.Error);
            }

            Place place = check3.Value!;
            IReadOnlyList<Review> items = await reviews.ListAsync(x => x.PlaceId == id).ConfigureAwait(false);
            decimal? average = items.Count == 0 ? null : decimal.Round(items.Average(x => (decimal)x.Rating), 2);
            return Result<PlaceDetails>.Success(new PlaceDetails(place, items, average));
        });
    }

    public Task<Result> ToggleFavoriteAsync(int placeId)
    {
        return operations.RunResultAsync("ToggleFavorite", async () =>
        {
            var check4 = session.TryRequireRole("Tourist");
            if (check4.IsFailure)
            {
                return new Failure(check4.Error);
            }

            int userId = check4.Value!.UserId;
            var check5 = await VisibleAsync(placeId).ConfigureAwait(false);
            if (check5.IsFailure)
            {
                return new Failure(check5.Error);
            }

            Favorite? current = (await favorites.ListAsync(x => x.UserId == userId && x.PlaceId == placeId).ConfigureAwait(false)).SingleOrDefault();
            if (current is null)
            {
                await favorites.AddAsync(new Favorite { UserId = userId, PlaceId = placeId }).ConfigureAwait(false);
            }
            else
            {
                await favorites.DeleteAsync(current).ConfigureAwait(false);
            }

            return Result.Success();
        });
    }

    public Task<Result> AddReviewAsync(int placeId, int rating, string comment)
    {
        return operations.RunResultAsync("LeaveReview", async () =>
        {
            var check6 = session.TryRequireRole("Tourist");
            if (check6.IsFailure)
            {
                return new Failure(check6.Error);
            }

            int userId = check6.Value!.UserId;
            if (rating < 1 || rating > 5)
            {
                return new Failure("Оцінка повинна бути від 1 до 5.");
            }

            var check7 = ServiceOperations.TryRequired(comment, "Відгук", 2000);
            if (check7.IsFailure)
            {
                return new Failure(check7.Error);
            }

            string validComment = check7.Value!;
            var check8 = await VisibleAsync(placeId).ConfigureAwait(false);
            if (check8.IsFailure)
            {
                return new Failure(check8.Error);
            }

            IReadOnlyList<Review> existing = await reviews.ListAsync(x => x.UserId == userId && x.PlaceId == placeId).ConfigureAwait(false);
            if (existing.Count != 0)
            {
                return new Failure("Ви вже залишили відгук для цього місця.");
            }

            await reviews.AddAsync(new Review { PlaceId = placeId, UserId = userId, Rating = rating, Comment = validComment, CreatedAt = DateTime.UtcNow }).ConfigureAwait(false);
            return Result.Success();
        });
    }

    private async Task<Result<Place>> VisibleAsync(int id)
    {
        Place? place = await places.FindAsync(id).ConfigureAwait(false);
        if (place is null)
        {
            return new Failure("Місце не знайдене.");
        }

        if (place.Status == "Hidden")
        {
            return new Failure("Місце недоступне.");
        }

        return Result<Place>.Success(place);
    }
}
