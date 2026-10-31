using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Contracts;

public interface ITrainReservationRepository
{
    // Отримати бронювання конкретної подорожі.
    Task<IReadOnlyList<TrainReservation>> ListForTripAsync(
        int tripId);

    // Отримати номери зайнятих місць у вибраному вагоні.
    Task<IReadOnlyList<int>> ListReservedSeatsAsync(
        int trainDepartureId,
        int wagonNumber);

    // Спробувати зберегти бронювання.
    // Повертає false, якщо місце вже забронював інший користувач.
    Task<bool> TryReserveAsync(
        TrainReservation reservation);
}