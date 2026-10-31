using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Contracts;

public interface IAccommodationReservationRepository
{
    Task<IReadOnlyList<AccommodationReservation>> ListForTripAsync(
        int tripId);

    Task<IReadOnlyList<int>> ListUnavailableIdsAsync(
        DateTime checkIn,
        DateTime checkOut);

    Task<bool> TryReserveAsync(
        AccommodationReservation reservation);
}