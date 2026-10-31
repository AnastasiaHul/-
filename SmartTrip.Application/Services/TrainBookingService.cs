using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class TrainBookingService(
    IRepository<TrainDeparture> departures,
    ITrainReservationRepository reservations,
    TripService trips,
    UserSession session,
    ServiceOperations operations)
{
    public Task<IReadOnlyList<TrainDeparture>> SearchAsync(
        string departureCity,
        string arrivalCity,
        DateTime date)
    {
        return operations.RunAsync<IReadOnlyList<TrainDeparture>>(
            "Train.Search",
            async () =>
            {
                session.RequireRole("Tourist");

                string from = Required(
                    departureCity,
                    "Вкажіть місто відправлення.",
                    100);

                string to = Required(
                    arrivalCity,
                    "Вкажіть місто прибуття.",
                    100);

                if (string.Equals(
                    from,
                    to,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "Міста відправлення та прибуття мають відрізнятися.");
                }

                if (date.Date < DateTime.Today
                    || date.Date > DateTime.Today.AddDays(59))
                {
                    throw new ArgumentException(
                        "Оберіть дату в межах найближчих 60 днів.");
                }

                DateTime start = DateTime.SpecifyKind(
                    date.Date,
                    DateTimeKind.Utc);

                DateTime end = start.AddDays(1);

                IReadOnlyList<TrainDeparture> result =
                    await departures.ListAsync(
                        x => x.DepartureAt >= start
                            && x.DepartureAt < end)
                    .ConfigureAwait(false);

                DateTime now = CurrentTime();

                return result
                    .Where(x =>
                        string.Equals(
                            x.DepartureCity,
                            from,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            x.ArrivalCity,
                            to,
                            StringComparison.OrdinalIgnoreCase)
                        && x.DepartureAt > now)
                    .OrderBy(x => x.DepartureAt)
                    .ToList();
            });
    }

    public Task<IReadOnlyList<int>> AvailableSeatsAsync(
        int trainDepartureId,
        int wagonNumber)
    {
        return operations.RunAsync<IReadOnlyList<int>>(
            "Train.AvailableSeats",
            async () =>
            {
                session.RequireRole("Tourist");

                TrainDeparture departure =
                    await FindDepartureAsync(trainDepartureId)
                        .ConfigureAwait(false);

                ValidateWagon(departure, wagonNumber);

                IReadOnlyList<int> reserved =
                    await reservations.ListReservedSeatsAsync(
                        trainDepartureId,
                        wagonNumber)
                    .ConfigureAwait(false);

                HashSet<int> occupied = reserved.ToHashSet();

                return Enumerable
                    .Range(1, departure.SeatsPerWagon)
                    .Where(seat => !occupied.Contains(seat))
                    .ToList();
            });
    }

    public Task<TrainReservation> ReserveAsync(
        int tripId,
        int trainDepartureId,
        int wagonNumber,
        int seatNumber,
        string passengerName)
    {
        return operations.RunAsync(
            "Train.Reserve",
            async () =>
            {
                session.RequireRole("Tourist");

                Trip trip = await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                TrainDeparture departure =
                    await FindDepartureAsync(trainDepartureId)
                        .ConfigureAwait(false);

                ValidateWagon(departure, wagonNumber);

                if (seatNumber < 1
                    || seatNumber > departure.SeatsPerWagon)
                {
                    throw new ArgumentException(
                        "Оберіть коректний номер місця.");
                }

                string passenger = Required(
                    passengerName,
                    "Вкажіть ім’я та прізвище пасажира.",
                    200);

                if (departure.DepartureAt.Date < trip.StartDate.Date
                    || departure.ArrivalAt.Date > trip.EndDate.Date)
                {
                    throw new ArgumentException(
                        "Поїздка поїздом має бути в межах дат подорожі.");
                }

                if (departure.TicketPrice <= 0
                    || departure.TicketPrice > 100_000_000m
                    || decimal.Round(departure.TicketPrice, 2)
                        != departure.TicketPrice)
                {
                    throw new ArgumentException(
                        "Для цього рейсу вказана некоректна ціна.");
                }

                var reservation = new TrainReservation
                {
                    TripId = trip.TripId,
                    TrainDepartureId = departure.TrainDepartureId,
                    WagonNumber = wagonNumber,
                    SeatNumber = seatNumber,
                    PassengerName = passenger,
                    BookingCode = $"ST-{Guid.NewGuid():N}",
                    TotalPrice = departure.TicketPrice,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Confirmed"
                };

                bool saved = await reservations.TryReserveAsync(
                    reservation)
                    .ConfigureAwait(false);

                if (!saved)
                {
                    throw new ArgumentException(
                        "Це місце вже заброньовано. Оберіть інше.");
                }

                return reservation;
            });
    }

    public Task<IReadOnlyList<TrainReservation>> ListAsync(
        int tripId)
    {
        return operations.RunAsync<IReadOnlyList<TrainReservation>>(
            "Train.ListReservations",
            async () =>
            {
                session.RequireRole("Tourist");

                await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                return await reservations.ListForTripAsync(tripId)
                    .ConfigureAwait(false);
            });
    }

    private async Task<TrainDeparture> FindDepartureAsync(
        int trainDepartureId)
    {
        TrainDeparture departure =
            await departures.FindAsync(trainDepartureId)
                .ConfigureAwait(false)
            ?? throw new ArgumentException(
                "Рейс не знайдено.");

        if (departure.DepartureAt <= CurrentTime())
        {
            throw new ArgumentException(
                "Бронювання цього рейсу вже закрито.");
        }

        if (departure.ArrivalAt <= departure.DepartureAt
            || departure.WagonCount < 1
            || departure.SeatsPerWagon < 1)
        {
            throw new ArgumentException(
                "Рейс має некоректні налаштування.");
        }

        return departure;
    }

    private static void ValidateWagon(
        TrainDeparture departure,
        int wagonNumber)
    {
        if (wagonNumber < 1
            || wagonNumber > departure.WagonCount)
        {
            throw new ArgumentException(
                "Оберіть коректний номер вагона.");
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