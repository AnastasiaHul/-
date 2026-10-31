using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Contracts;

public interface IExcursionReservationRepository
{
    Task<IReadOnlyList<ExcursionReservation>> ListForTripAsync(
        int tripId);

    Task<IReadOnlyDictionary<int, long>> GetReservedCountsAsync(
        IReadOnlyList<int> sessionIds);

    Task<bool> TryReserveAsync(
        ExcursionReservation reservation);
}