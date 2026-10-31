using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Contracts;

public interface ICarReservationRepository
{
    Task<IReadOnlyList<CarReservation>> ListForTripAsync(
        int tripId);

    Task<IReadOnlyList<int>> ListUnavailableIdsAsync(
        DateTime pickupDate,
        DateTime returnDate);

    Task<bool> TryReserveAsync(
        CarReservation reservation);
}