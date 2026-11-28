using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed record TripCopyResult(Trip Trip, int Visits, int SkippedVisits, int PackingItems);

public sealed class TripCopyService(
    ITripCopyRepository copies,
    IRepository<ItineraryVisit> visits,
    IRepository<PackingItem> packing,
    IRepository<Place> places,
    TripService trips,
    ServiceOperations operations)
{
    public Task<Result<TripCopyResult>> CopyAsync(int tripId, string title, DateTime newStart)
    {
        return operations.RunResultAsync<TripCopyResult>("Trip.Copy", async () =>
        {
            var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (owned.IsFailure)
            {
                return new Failure(owned.Error);
            }

            var validTitle = ServiceOperations.TryRequired(title, "Назва", 150);
            if (validTitle.IsFailure)
            {
                return new Failure(validTitle.Error);
            }

            if (newStart.Date < DateTime.Today || newStart.Date > DateTime.Today.AddYears(5))
            {
                return new Failure("Нова дата початку: від сьогодні до п’яти років наперед.");
            }

            Trip source = owned.Value!;
            TimeSpan shift = newStart.Date - source.StartDate.Date;
            var copy = new Trip
            {
                UserId = source.UserId,
                Title = validTitle.Value!,
                City = source.City,
                StartDate = DateTime.SpecifyKind(newStart.Date, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(source.EndDate.Date + shift, DateTimeKind.Utc),
                Budget = source.Budget,
                PeopleCount = source.PeopleCount,
                Interests = source.Interests
            };

            var newVisits = new List<ItineraryVisit>();
            int skipped = 0;
            foreach (ItineraryVisit visit in await visits.ListAsync(x => x.TripId == tripId).ConfigureAwait(false))
            {
                Place? place = await places.FindAsync(visit.PlaceId).ConfigureAwait(false);
                if (place is not { Status: "Published" or "Active" })
                {
                    skipped++;
                    continue;
                }

                newVisits.Add(new ItineraryVisit
                {
                    PlaceId = visit.PlaceId,
                    PlaceName = place.Name,
                    VisitAt = DateTime.SpecifyKind(visit.VisitAt + shift, DateTimeKind.Utc),
                    Notes = visit.Notes
                });
            }

            // Речі копіюються як ще не спаковані.
            var newItems = (await packing.ListAsync(x => x.TripId == tripId).ConfigureAwait(false))
                .Select(x => new PackingItem { Name = x.Name })
                .ToList();

            await copies.AddAsync(copy, newVisits, newItems).ConfigureAwait(false);
            return new TripCopyResult(copy, newVisits.Count, skipped, newItems.Count);
        });
    }
}
