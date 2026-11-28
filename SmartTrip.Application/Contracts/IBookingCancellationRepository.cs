namespace SmartTrip.Application.Contracts;

public enum BookingKind
{
    Train,
    Housing,
    Car,
    Excursion
}

public interface IBookingCancellationRepository
{
    // Скасовує бронювання та прибирає пов’язану з ним витрату однією транзакцією.
    // Повертає false, якщо підтвердженого бронювання вже немає.
    Task<bool> TryCancelAsync(
        BookingKind kind,
        int reservationId,
        int tripId,
        string expenseMarker);
}
