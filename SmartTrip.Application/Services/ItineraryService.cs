using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace SmartTrip.Application.Services;
public sealed class ItineraryService(IRepository<ItineraryVisit> visits, IRepository<Place> places, TripService trips, ServiceOperations operations)
{
    public Task<Result<IReadOnlyList<ItineraryVisit>>> ListAsync(int tripId)
    {
        return operations.RunResultAsync<IReadOnlyList<ItineraryVisit>>("ViewItinerary", async () =>
        {
            var check1 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check1.IsFailure)
            {
                return new Failure(check1.Error);
            }

            // Отримуємо відвідування цієї подорожі.
            IReadOnlyList<ItineraryVisit> items = await visits.ListAsync(visit => visit.TripId == tripId).ConfigureAwait(false);
            // Повертаємо розклад за датою та часом.
            return Result<IReadOnlyList<ItineraryVisit>>.Success(items.OrderBy(visit => visit.VisitAt).ToList());
        });
    }

    public Task<Result> AddAsync(int tripId, int placeId, DateTime visitAt, string notes)
    {
        return operations.RunResultAsync("PlanVisit", async () =>
        {
            var check2 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check2.IsFailure)
            {
                return new Failure(check2.Error);
            }

            Trip trip = check2.Value!;
            if (visitAt == default || visitAt.Date < trip.StartDate.Date || visitAt.Date > trip.EndDate.Date)
            {
                return new Failure("Дата відвідування повинна бути " + "в межах подорожі.");
            }

            Place? place = await places.FindAsync(placeId).ConfigureAwait(false);
            if (place is null)
            {
                return new Failure("Місце не знайдено.");
            }

            if (place.Status == "Hidden")
            {
                return new Failure("Це місце зараз недоступне.");
            }

            if (notes.Length > 1000)
            {
                return new Failure("Примітка: максимум 1000 символів.");
            }

            DateTime time = DateTime.SpecifyKind(visitAt, DateTimeKind.Utc);
            IReadOnlyList<ItineraryVisit> existing = await visits.ListAsync(visit => visit.TripId == tripId && visit.VisitAt == time).ConfigureAwait(false);
            if (existing.Count != 0)
            {
                return new Failure("На цей час уже заплановано " + "інше відвідування.");
            }

            var visit = new ItineraryVisit
            {
                TripId = tripId,
                PlaceId = placeId,
                PlaceName = place.Name,
                VisitAt = time,
                Notes = notes.Trim()
            };
            await visits.AddAsync(visit).ConfigureAwait(false);
            return Result.Success();
        });
    }

    public Task<Result> RemoveAsync(int visitId)
    {
        return operations.RunResultAsync("RemovePlannedVisit", async () =>
        {
            ItineraryVisit? visit = await visits.FindAsync(visitId).ConfigureAwait(false);
            if (visit is null)
            {
                return new Failure("Відвідування не знайдено.");
            }

            var check3 = await trips.OwnedAsync(visit.TripId).ConfigureAwait(false);
            if (check3.IsFailure)
            {
                return new Failure(check3.Error);
            }

            await visits.DeleteAsync(visit).ConfigureAwait(false);
            return Result.Success();
        });
    }
}
