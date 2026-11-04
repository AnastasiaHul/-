using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace SmartTrip.Application.Services;
public sealed class TrainBookingService(IRepository<TrainDeparture> departures, ITrainReservationRepository reservations, TripService trips, UserSession session, ServiceOperations operations)
{
    public Task<Result<IReadOnlyList<TrainDeparture>>> SearchAsync(string departureCity, string arrivalCity, DateTime date)
    {
        return operations.RunResultAsync<IReadOnlyList<TrainDeparture>>("Train.Search", async () =>
        {
            var check1 = session.TryRequireRole("Tourist");
            if (check1.IsFailure)
            {
                return new Failure(check1.Error);
            }

            var check2 = Required(departureCity, "Вкажіть місто відправлення.", 100);
            if (check2.IsFailure)
            {
                return new Failure(check2.Error);
            }

            string from = check2.Value!;
            var check3 = Required(arrivalCity, "Вкажіть місто прибуття.", 100);
            if (check3.IsFailure)
            {
                return new Failure(check3.Error);
            }

            string to = check3.Value!;
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                return new Failure("Міста відправлення та прибуття мають відрізнятися.");
            }

            if (date.Date < DateTime.Today || date.Date > DateTime.Today.AddDays(59))
            {
                return new Failure("Оберіть дату в межах найближчих 60 днів.");
            }

            DateTime start = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            DateTime end = start.AddDays(1);
            IReadOnlyList<TrainDeparture> result = await departures.ListAsync(x => x.DepartureAt >= start && x.DepartureAt < end).ConfigureAwait(false);
            DateTime now = CurrentTime();
            return Result<IReadOnlyList<TrainDeparture>>.Success(result.Where(x => string.Equals(x.DepartureCity, from, StringComparison.OrdinalIgnoreCase) && string.Equals(x.ArrivalCity, to, StringComparison.OrdinalIgnoreCase) && x.DepartureAt > now).OrderBy(x => x.DepartureAt).ToList());
        });
    }

    public Task<Result<IReadOnlyList<int>>> AvailableSeatsAsync(int trainDepartureId, int wagonNumber)
    {
        return operations.RunResultAsync<IReadOnlyList<int>>("Train.AvailableSeats", async () =>
        {
            var check4 = session.TryRequireRole("Tourist");
            if (check4.IsFailure)
            {
                return new Failure(check4.Error);
            }

            var check5 = await FindDepartureAsync(trainDepartureId).ConfigureAwait(false);
            if (check5.IsFailure)
            {
                return new Failure(check5.Error);
            }

            TrainDeparture departure = check5.Value!;
            var check6 = ValidateWagon(departure, wagonNumber);
            if (check6.IsFailure)
            {
                return new Failure(check6.Error);
            }

            IReadOnlyList<int> reserved = await reservations.ListReservedSeatsAsync(trainDepartureId, wagonNumber).ConfigureAwait(false);
            HashSet<int> occupied = reserved.ToHashSet();
            return Result<IReadOnlyList<int>>.Success(Enumerable.Range(1, departure.SeatsPerWagon).Where(seat => !occupied.Contains(seat)).ToList());
        });
    }

    public Task<Result<TrainReservation>> ReserveAsync(int tripId, int trainDepartureId, int wagonNumber, int seatNumber, string passengerName)
    {
        return operations.RunResultAsync<TrainReservation>("Train.Reserve", async () =>
        {
            var check7 = session.TryRequireRole("Tourist");
            if (check7.IsFailure)
            {
                return new Failure(check7.Error);
            }

            var check8 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check8.IsFailure)
            {
                return new Failure(check8.Error);
            }

            Trip trip = check8.Value!;
            var check9 = await FindDepartureAsync(trainDepartureId).ConfigureAwait(false);
            if (check9.IsFailure)
            {
                return new Failure(check9.Error);
            }

            TrainDeparture departure = check9.Value!;
            var check10 = ValidateWagon(departure, wagonNumber);
            if (check10.IsFailure)
            {
                return new Failure(check10.Error);
            }

            if (seatNumber < 1 || seatNumber > departure.SeatsPerWagon)
            {
                return new Failure("Оберіть коректний номер місця.");
            }

            var check11 = Required(passengerName, "Вкажіть ім’я та прізвище пасажира.", 200);
            if (check11.IsFailure)
            {
                return new Failure(check11.Error);
            }

            string passenger = check11.Value!;
            if (departure.DepartureAt.Date < trip.StartDate.Date || departure.ArrivalAt.Date > trip.EndDate.Date)
            {
                return new Failure("Поїздка поїздом має бути в межах дат подорожі.");
            }

            if (departure.TicketPrice <= 0 || departure.TicketPrice > 100_000_000m || decimal.Round(departure.TicketPrice, 2) != departure.TicketPrice)
            {
                return new Failure("Для цього рейсу вказана некоректна ціна.");
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
            bool saved = await reservations.TryReserveAsync(reservation).ConfigureAwait(false);
            if (!saved)
            {
                return new Failure("Це місце вже заброньовано. Оберіть інше.");
            }

            return Result<TrainReservation>.Success(reservation);
        });
    }

    public Task<Result<IReadOnlyList<TrainReservation>>> ListAsync(int tripId)
    {
        return operations.RunResultAsync<IReadOnlyList<TrainReservation>>("Train.ListReservations", async () =>
        {
            var check12 = session.TryRequireRole("Tourist");
            if (check12.IsFailure)
            {
                return new Failure(check12.Error);
            }

            var check13 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check13.IsFailure)
            {
                return new Failure(check13.Error);
            }

            return Result<IReadOnlyList<TrainReservation>>.Success(await reservations.ListForTripAsync(tripId).ConfigureAwait(false));
        });
    }

    private async Task<Result<TrainDeparture>> FindDepartureAsync(int trainDepartureId)
    {
        TrainDeparture? departure = await departures.FindAsync(trainDepartureId).ConfigureAwait(false);
        if (departure is null)
        {
            return new Failure("Рейс не знайдено.");
        }

        if (departure.DepartureAt <= CurrentTime())
        {
            return new Failure("Бронювання цього рейсу вже закрито.");
        }

        if (departure.ArrivalAt <= departure.DepartureAt || departure.WagonCount < 1 || departure.SeatsPerWagon < 1)
        {
            return new Failure("Рейс має некоректні налаштування.");
        }

        return Result<TrainDeparture>.Success(departure);
    }

    private static Result ValidateWagon(TrainDeparture departure, int wagonNumber)
    {
        if (wagonNumber < 1 || wagonNumber > departure.WagonCount)
        {
            return new Failure("Оберіть коректний номер вагона.");
        }

        return Result.Success();
    }

    private static Result<string> Required(string value, string message, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Failure(message);
        }

        string result = value.Trim();
        if (result.Length > maxLength)
        {
            return new Failure($"Максимальна довжина — {maxLength} символів.");
        }

        return Result<string>.Success(result);
    }

    private static DateTime CurrentTime()
    {
        return DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
    }
}
