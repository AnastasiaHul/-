using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed record PlaceRecommendation(
    int PlaceId,
    string Name,
    string Category,
    decimal Price,
    string Address,
    decimal? Rating,
    int InterestMatches,
    string Reason);

public sealed class RecommendationService(
    IRepository<Place> places,
    IRepository<Review> reviews,
    IRepository<ItineraryVisit> visits,
    IRepository<Expense> expenses,
    TripService trips,
    ServiceOperations operations)
{
    private static readonly char[] Separators = [' ', ',', ';', '.', '/', '\n', '\r', '\t'];

    public Task<Result<IReadOnlyList<PlaceRecommendation>>> GetAsync(int tripId)
    {
        return operations.RunResultAsync<IReadOnlyList<PlaceRecommendation>>("Place.Recommend", async () =>
        {
            var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (owned.IsFailure)
            {
                return new Failure(owned.Error);
            }

            Trip trip = owned.Value!;
            decimal spent = (await expenses.ListAsync(x => x.TripId == tripId).ConfigureAwait(false))
                .Sum(x => x.Amount);
            decimal remaining = Math.Max(0, trip.Budget - spent);
            var planned = (await visits.ListAsync(x => x.TripId == tripId).ConfigureAwait(false))
                .Select(x => x.PlaceId)
                .ToHashSet();
            string city = trip.City.Trim();
            string[] interests = trip.Interests
                .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var candidates = (await places
                    .ListAsync(x => x.Status == "Published" || x.Status == "Active")
                    .ConfigureAwait(false))
                .Where(x => !planned.Contains(x.PlaceId)
                    && x.Price <= remaining
                    && (x.Address?.Contains(city, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

            var result = new List<PlaceRecommendation>();
            foreach (Place place in candidates)
            {
                int placeId = place.PlaceId;
                var marks = await reviews.ListAsync(x => x.PlaceId == placeId).ConfigureAwait(false);
                decimal? rating = marks.Count == 0
                    ? null
                    : decimal.Round(marks.Average(x => (decimal)x.Rating), 1);
                string text = $"{place.Name} {place.Category} {place.Description}";
                string[] matches = interests
                    .Where(x => text.Contains(x, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                string reason = matches.Length > 0
                    ? "Інтереси: " + string.Join(", ", matches)
                    : place.Price == 0 ? "Безкоштовно у вашому місті" : "У вашому місті та в межах бюджету";
                result.Add(new PlaceRecommendation(placeId, place.Name, place.Category, place.Price,
                    place.Address!, rating, matches.Length, reason));
            }

            return Result<IReadOnlyList<PlaceRecommendation>>.Success(result
                .OrderByDescending(x => x.InterestMatches)
                .ThenByDescending(x => x.Rating ?? 0)
                .ThenBy(x => x.Price)
                .Take(20)
                .ToList());
        });
    }
}
