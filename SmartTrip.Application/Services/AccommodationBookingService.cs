using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class AccommodationBookingService(
    IRepository<Accommodation> accommodations,
    IAccommodationReservationRepository reservations,
    TripService trips,
    UserSession session,
    ServiceOperations operations)
{
    public Task<IReadOnlyList<Accommodation>> SearchAsync(
        string city,
        DateTime checkIn,
        DateTime checkOut,
        int guestsCount)
    {
        return operations.RunAsync<IReadOnlyList<Accommodation>>(
            "Accommodation.Search",
            async () =>
            {
                session.RequireRole("Tourist");

                string selectedCity = Required(
                    city,
                    "Вкажіть місто.",
                    100);

                var dates = ValidateStay(
                    checkIn,
                    checkOut,
                    guestsCount);

                IReadOnlyList<Accommodation> available =
                    await accommodations.ListAsync(
                        x => x.IsActive
                            && x.MaxGuests >= guestsCount)
                    .ConfigureAwait(false);

                IReadOnlyList<int> unavailableIds =
                    await reservations.ListUnavailableIdsAsync(
                        dates.CheckIn,
                        dates.CheckOut)
                    .ConfigureAwait(false);

                HashSet<int> unavailable =
                    unavailableIds.ToHashSet();

                return available
                    .Where(x =>
                        string.Equals(
                            x.City,
                            selectedCity,
                            StringComparison.OrdinalIgnoreCase)
                        && !unavailable.Contains(x.AccommodationId))
                    .OrderBy(x => x.PricePerNight)
                    .ToList();
            });
    }

    public Task<AccommodationReservation> ReserveAsync(
        int tripId,
        int accommodationId,
        DateTime checkIn,
        DateTime checkOut,
        int guestsCount,
        string guestName)
    {
        return operations.RunAsync(
            "Accommodation.Reserve",
            async () =>
            {
                session.RequireRole("Tourist");

                Trip trip = await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                var dates = ValidateStay(
                    checkIn,
                    checkOut,
                    guestsCount);

                if (dates.CheckIn.Date < trip.StartDate.Date
                    || dates.CheckOut.Date > trip.EndDate.Date)
                {
                    throw new ArgumentException(
                        "Дати проживання мають бути в межах дат подорожі.");
                }

                string name = Required(
                    guestName,
                    "Вкажіть ім’я та прізвище гостя.",
                    200);

                Accommodation accommodation =
                    await accommodations.FindAsync(accommodationId)
                        .ConfigureAwait(false)
                    ?? throw new ArgumentException(
                        "Житло не знайдено.");

                if (!accommodation.IsActive)
                {
                    throw new ArgumentException(
                        "Це житло більше недоступне для бронювання.");
                }

                if (guestsCount > accommodation.MaxGuests)
                {
                    throw new ArgumentException(
                        $"Максимальна кількість гостей: "
                        + $"{accommodation.MaxGuests}.");
                }

                decimal nightlyPrice =
                    accommodation.PricePerNight;

                if (nightlyPrice <= 0
                    || nightlyPrice > 100_000_000m
                    || decimal.Round(nightlyPrice, 2)
                        != nightlyPrice)
                {
                    throw new ArgumentException(
                        "Для цього житла вказана некоректна ціна.");
                }

                int nights =
                    (dates.CheckOut - dates.CheckIn).Days;

                decimal totalPrice = nightlyPrice * nights;

                if (totalPrice > 100_000_000m)
                {
                    throw new ArgumentException(
                        "Загальна вартість бронювання надто велика.");
                }

                var reservation =
                    new AccommodationReservation
                    {
                        TripId = trip.TripId,
                        AccommodationId =
                            accommodation.AccommodationId,
                        CheckIn = dates.CheckIn,
                        CheckOut = dates.CheckOut,
                        GuestsCount = guestsCount,
                        GuestName = name,
                        BookingCode = $"HT-{Guid.NewGuid():N}",
                        TotalPrice = totalPrice,
                        CreatedAt = DateTime.UtcNow,
                        Status = "Confirmed"
                    };

                bool saved = await reservations.TryReserveAsync(
                    reservation)
                    .ConfigureAwait(false);

                if (!saved)
                {
                    throw new ArgumentException(
                        "Це житло вже заброньовано на вибрані дати. "
                        + "Повторіть пошук або змініть дати.");
                }

                return reservation;
            });
    }

    public Task<IReadOnlyList<AccommodationReservation>> ListAsync(
        int tripId)
    {
        return operations
            .RunAsync<IReadOnlyList<AccommodationReservation>>(
                "Accommodation.ListReservations",
                async () =>
                {
                    session.RequireRole("Tourist");

                    await trips.OwnedAsync(tripId)
                        .ConfigureAwait(false);

                    return await reservations.ListForTripAsync(
                        tripId)
                        .ConfigureAwait(false);
                });
    }

    private static (DateTime CheckIn, DateTime CheckOut)
        ValidateStay(
            DateTime checkIn,
            DateTime checkOut,
            int guestsCount)
    {
        DateTime start = checkIn.Date;
        DateTime end = checkOut.Date;

        if (start < DateTime.Today)
        {
            throw new ArgumentException(
                "Дата заїзду не може бути в минулому.");
        }

        if (start > DateTime.Today.AddDays(365))
        {
            throw new ArgumentException(
                "Бронювання доступне на найближчі 365 днів.");
        }

        if (end <= start)
        {
            throw new ArgumentException(
                "Дата виїзду має бути пізніше дати заїзду.");
        }

        if ((end - start).Days > 30)
        {
            throw new ArgumentException(
                "Одне бронювання може тривати до 30 ночей.");
        }

        if (guestsCount < 1 || guestsCount > 20)
        {
            throw new ArgumentException(
                "Кількість гостей має бути від 1 до 20.");
        }

        return (
            DateTime.SpecifyKind(start, DateTimeKind.Utc),
            DateTime.SpecifyKind(end, DateTimeKind.Utc));
    }

    private static string Required(
        string value,
        string message,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }

        string result = value.Trim();

        if (result.Length > maxLength)
        {
            throw new ArgumentException(
                $"Максимальна довжина — {maxLength} символів.");
        }

        return result;
    }
}