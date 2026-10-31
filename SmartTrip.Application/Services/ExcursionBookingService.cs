using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed record ExcursionOffer(
    ExcursionSession Session,
    int AvailableSeats);

public sealed class ExcursionBookingService(
    IRepository<ExcursionSession> excursions,
    IExcursionReservationRepository reservations,
    TripService trips,
    UserSession session,
    ServiceOperations operations)
{
    public Task<IReadOnlyList<ExcursionOffer>> SearchAsync(
        string city,
        DateTime date,
        int participantsCount)
    {
        return operations.RunAsync<IReadOnlyList<ExcursionOffer>>(
            "Excursion.Search",
            async () =>
            {
                session.RequireRole("Tourist");

                string selectedCity = Required(
                    city,
                    "Вкажіть місто.",
                    100);

                ValidateParticipants(participantsCount);
                ValidateDate(date);

                DateTime start = DateTime.SpecifyKind(
                    date.Date,
                    DateTimeKind.Utc);

                DateTime end = start.AddDays(1);

                IReadOnlyList<ExcursionSession> found =
                    await excursions.ListAsync(
                        x => x.IsActive
                            && x.StartsAt >= start
                            && x.StartsAt < end)
                    .ConfigureAwait(false);

                DateTime now = CurrentTime();

                List<ExcursionSession> matching = found
                    .Where(x =>
                        string.Equals(
                            x.City,
                            selectedCity,
                            StringComparison.OrdinalIgnoreCase)
                        && x.StartsAt > now
                        && x.Capacity > 0
                        && x.DurationMinutes > 0
                        && x.DurationMinutes <= 720)
                    .OrderBy(x => x.StartsAt)
                    .ToList();

                int[] ids = matching
                    .Select(x => x.ExcursionSessionId)
                    .ToArray();

                IReadOnlyDictionary<int, long> reservedCounts =
                    await reservations.GetReservedCountsAsync(ids)
                        .ConfigureAwait(false);

                var result = new List<ExcursionOffer>();

                foreach (ExcursionSession excursion in matching)
                {
                    reservedCounts.TryGetValue(
                        excursion.ExcursionSessionId,
                        out long reserved);

                    long available =
                        (long)excursion.Capacity - reserved;

                    if (available >= participantsCount)
                    {
                        result.Add(new ExcursionOffer(
                            excursion,
                            (int)available));
                    }
                }

                return result;
            });
    }

    public Task<ExcursionReservation> ReserveAsync(
        int tripId,
        int excursionSessionId,
        int participantsCount,
        string guestName)
    {
        return operations.RunAsync(
            "Excursion.Reserve",
            async () =>
            {
                session.RequireRole("Tourist");

                ValidateParticipants(participantsCount);

                string name = Required(
                    guestName,
                    "Вкажіть ім’я та прізвище відповідального гостя.",
                    200);

                Trip trip = await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                ExcursionSession excursion =
                    await excursions.FindAsync(excursionSessionId)
                        .ConfigureAwait(false)
                    ?? throw new ArgumentException(
                        "Екскурсію не знайдено.");

                if (!excursion.IsActive)
                {
                    throw new ArgumentException(
                        "Екскурсія більше недоступна для бронювання.");
                }

                ValidateDate(excursion.StartsAt);

                if (excursion.StartsAt <= CurrentTime())
                {
                    throw new ArgumentException(
                        "Ця екскурсія вже почалася.");
                }

                if (excursion.DurationMinutes < 1
                    || excursion.DurationMinutes > 720
                    || excursion.Capacity < 1)
                {
                    throw new ArgumentException(
                        "Екскурсія має некоректні налаштування.");
                }

                DateTime endsAt = excursion.StartsAt.AddMinutes(
                    excursion.DurationMinutes);

                if (excursion.StartsAt.Date < trip.StartDate.Date
                    || endsAt.Date > trip.EndDate.Date)
                {
                    throw new ArgumentException(
                        "Екскурсія має проходити в межах дат подорожі.");
                }

                if (participantsCount > excursion.Capacity)
                {
                    throw new ArgumentException(
                        "Кількість учасників перевищує "
                        + "місткість екскурсії.");
                }

                decimal price = excursion.PricePerPerson;

                if (price <= 0
                    || price > 100_000_000m
                    || decimal.Round(price, 2) != price)
                {
                    throw new ArgumentException(
                        "Для екскурсії вказана некоректна ціна.");
                }

                decimal totalPrice = price * participantsCount;

                if (totalPrice > 100_000_000m)
                {
                    throw new ArgumentException(
                        "Загальна вартість бронювання надто велика.");
                }

                var reservation = new ExcursionReservation
                {
                    TripId = trip.TripId,
                    ExcursionSessionId =
                        excursion.ExcursionSessionId,
                    ParticipantsCount = participantsCount,
                    GuestName = name,
                    BookingCode = $"EX-{Guid.NewGuid():N}",
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
                        "Недостатньо вільних місць або екскурсія "
                        + "вже недоступна. Повторіть пошук.");
                }

                return reservation;
            });
    }

    public Task<IReadOnlyList<ExcursionReservation>> ListAsync(
        int tripId)
    {
        return operations
            .RunAsync<IReadOnlyList<ExcursionReservation>>(
                "Excursion.ListReservations",
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

    private static void ValidateParticipants(int count)
    {
        if (count < 1 || count > 1000)
        {
            throw new ArgumentException(
                "Кількість учасників має бути від 1 до 1000.");
        }
    }

    private static void ValidateDate(DateTime date)
    {
        if (date.Date < DateTime.Today
            || date.Date > DateTime.Today.AddDays(59))
        {
            throw new ArgumentException(
                "Оберіть дату в межах найближчих 60 днів.");
        }
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

    private static DateTime CurrentTime()
    {
        return DateTime.SpecifyKind(
            DateTime.Now,
            DateTimeKind.Utc);
    }
}