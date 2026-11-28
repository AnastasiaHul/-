using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class VisitRescheduleService(
    IRepository<ItineraryVisit> visits,
    TripService trips,
    ServiceOperations operations)
{
    public Task<Result> RescheduleAsync(int visitId, DateTime visitAt, string notes)
    {
        return operations.RunResultAsync("Itinerary.Reschedule", async () =>
        {
            ItineraryVisit? visit = await visits.FindAsync(visitId).ConfigureAwait(false);
            if (visit is null)
            {
                return new Failure("Відвідування не знайдено.");
            }

            var owned = await trips.OwnedAsync(visit.TripId).ConfigureAwait(false);
            if (owned.IsFailure)
            {
                return new Failure(owned.Error);
            }

            Trip trip = owned.Value!;
            if (visitAt.Date < trip.StartDate.Date || visitAt.Date > trip.EndDate.Date)
            {
                return new Failure("Дата відвідування повинна бути в межах подорожі.");
            }

            if (notes.Length > 1000)
            {
                return new Failure("Примітка: максимум 1000 символів.");
            }

            DateTime time = DateTime.SpecifyKind(visitAt, DateTimeKind.Utc);
            int tripId = visit.TripId;
            var sameTime = await visits
                .ListAsync(x => x.TripId == tripId && x.VisitAt == time && x.ItineraryVisitId != visitId)
                .ConfigureAwait(false);
            if (sameTime.Count != 0)
            {
                return new Failure("На цей час уже заплановано інше відвідування.");
            }

            visit.VisitAt = time;
            visit.Notes = notes.Trim();
            await visits.UpdateAsync(visit).ConfigureAwait(false);
            return Result.Success();
        });
    }
}
