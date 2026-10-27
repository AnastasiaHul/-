using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class ItineraryService(
    IRepository<ItineraryVisit> visits,
    IRepository<Place> places,
    TripService trips,
    ServiceOperations operations)
{
    public Task<IReadOnlyList<ItineraryVisit>> ListAsync(int tripId)
    {
        return operations.RunAsync<IReadOnlyList<ItineraryVisit>>(
            "ViewItinerary",
            async () =>
            {
                // Перевіряємо власника подорожі.
                await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                // Отримуємо відвідування цієї подорожі.
                IReadOnlyList<ItineraryVisit> items =
                    await visits.ListAsync(
                        visit => visit.TripId == tripId)
                    .ConfigureAwait(false);

                // Повертаємо розклад за датою та часом.
                return items
                    .OrderBy(visit => visit.VisitAt)
                    .ToList();
            });
    }

    public Task AddAsync(
        int tripId,
        int placeId,
        DateTime visitAt,
        string notes)
    {
        return operations.RunAsync(
            "PlanVisit",
            async () =>
            {
                Trip trip = await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                if (visitAt == default
                    || visitAt.Date < trip.StartDate.Date
                    || visitAt.Date > trip.EndDate.Date)
                {
                    throw new ArgumentException(
                        "Дата відвідування повинна бути "
                        + "в межах подорожі.");
                }

                Place place = await places.FindAsync(placeId)
                    .ConfigureAwait(false)
                    ?? throw new ArgumentException(
                        "Місце не знайдено.");

                if (place.Status == "Hidden")
                {
                    throw new ArgumentException(
                        "Це місце зараз недоступне.");
                }

                if (notes.Length > 1000)
                {
                    throw new ArgumentException(
                        "Примітка: максимум 1000 символів.");
                }

                DateTime time = DateTime.SpecifyKind(
                    visitAt,
                    DateTimeKind.Utc);

                IReadOnlyList<ItineraryVisit> existing =
                    await visits.ListAsync(
                        visit => visit.TripId == tripId
                            && visit.VisitAt == time)
                    .ConfigureAwait(false);

                if (existing.Count != 0)
                {
                    throw new ArgumentException(
                        "На цей час уже заплановано "
                        + "інше відвідування.");
                }

                var visit = new ItineraryVisit
                {
                    TripId = tripId,
                    PlaceId = placeId,
                    PlaceName = place.Name,
                    VisitAt = time,
                    Notes = notes.Trim()
                };

                await visits.AddAsync(visit)
                    .ConfigureAwait(false);
            });
    }

    public Task RemoveAsync(int visitId)
    {
        return operations.RunAsync(
            "RemovePlannedVisit",
            async () =>
            {
                ItineraryVisit visit =
                    await visits.FindAsync(visitId)
                    .ConfigureAwait(false)
                    ?? throw new ArgumentException(
                        "Відвідування не знайдено.");

                // Прибирати можна лише зі своєї подорожі.
                await trips.OwnedAsync(visit.TripId)
                    .ConfigureAwait(false);

                await visits.DeleteAsync(visit)
                    .ConfigureAwait(false);
            });
    }
}