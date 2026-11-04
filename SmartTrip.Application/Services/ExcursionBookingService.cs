using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace SmartTrip.Application.Services;
public sealed record ExcursionOffer(ExcursionSession Session, int AvailableSeats);
public sealed class ExcursionBookingService(IRepository<ExcursionSession> excursions, IExcursionReservationRepository reservations, TripService trips, UserSession session, ServiceOperations operations)
{
    public Task<Result<IReadOnlyList<ExcursionOffer>>> SearchAsync(string city, DateTime date, int participantsCount)
    {
        return operations.RunResultAsync<IReadOnlyList<ExcursionOffer>>("Excursion.Search", async () =>
        {
            var check1 = session.TryRequireRole("Tourist");
            if (check1.IsFailure)
            {
                return new Failure(check1.Error);
            }

            var check2 = Required(city, "Вкажіть місто.", 100);
            if (check2.IsFailure)
            {
                return new Failure(check2.Error);
            }

            string selectedCity = check2.Value!;
            var check3 = ValidateParticipants(participantsCount);
            if (check3.IsFailure)
            {
                return new Failure(check3.Error);
            }

            var check4 = ValidateDate(date);
            if (check4.IsFailure)
            {
                return new Failure(check4.Error);
            }

            DateTime start = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            DateTime end = start.AddDays(1);
            IReadOnlyList<ExcursionSession> found = await excursions.ListAsync(x => x.IsActive && x.StartsAt >= start && x.StartsAt < end).ConfigureAwait(false);
            DateTime now = CurrentTime();
            List<ExcursionSession> matching = found.Where(x => string.Equals(x.City, selectedCity, StringComparison.OrdinalIgnoreCase) && x.StartsAt > now && x.Capacity > 0 && x.DurationMinutes > 0 && x.DurationMinutes <= 720).OrderBy(x => x.StartsAt).ToList();
            int[] ids = matching.Select(x => x.ExcursionSessionId).ToArray();
            IReadOnlyDictionary<int, long> reservedCounts = await reservations.GetReservedCountsAsync(ids).ConfigureAwait(false);
            var result = new List<ExcursionOffer>();
            foreach (ExcursionSession excursion in matching)
            {
                reservedCounts.TryGetValue(excursion.ExcursionSessionId, out long reserved);
                long available = (long)excursion.Capacity - reserved;
                if (available >= participantsCount)
                {
                    result.Add(new ExcursionOffer(excursion, (int)available));
                }
            }

            return Result<IReadOnlyList<ExcursionOffer>>.Success(result);
        });
    }

    public Task<Result<ExcursionReservation>> ReserveAsync(int tripId, int excursionSessionId, int participantsCount, string guestName)
    {
        return operations.RunResultAsync<ExcursionReservation>("Excursion.Reserve", async () =>
        {
            var check5 = session.TryRequireRole("Tourist");
            if (check5.IsFailure)
            {
                return new Failure(check5.Error);
            }

            var check6 = ValidateParticipants(participantsCount);
            if (check6.IsFailure)
            {
                return new Failure(check6.Error);
            }

            var check7 = Required(guestName, "Вкажіть ім’я та прізвище відповідального гостя.", 200);
            if (check7.IsFailure)
            {
                return new Failure(check7.Error);
            }

            string name = check7.Value!;
            var check8 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check8.IsFailure)
            {
                return new Failure(check8.Error);
            }

            Trip trip = check8.Value!;
            ExcursionSession? excursion = await excursions.FindAsync(excursionSessionId).ConfigureAwait(false);
            if (excursion is null)
            {
                return new Failure("Екскурсію не знайдено.");
            }

            if (!excursion.IsActive)
            {
                return new Failure("Екскурсія більше недоступна для бронювання.");
            }

            var check9 = ValidateDate(excursion.StartsAt);
            if (check9.IsFailure)
            {
                return new Failure(check9.Error);
            }

            if (excursion.StartsAt <= CurrentTime())
            {
                return new Failure("Ця екскурсія вже почалася.");
            }

            if (excursion.DurationMinutes < 1 || excursion.DurationMinutes > 720 || excursion.Capacity < 1)
            {
                return new Failure("Екскурсія має некоректні налаштування.");
            }

            DateTime endsAt = excursion.StartsAt.AddMinutes(excursion.DurationMinutes);
            if (excursion.StartsAt.Date < trip.StartDate.Date || endsAt.Date > trip.EndDate.Date)
            {
                return new Failure("Екскурсія має проходити в межах дат подорожі.");
            }

            if (participantsCount > excursion.Capacity)
            {
                return new Failure("Кількість учасників перевищує " + "місткість екскурсії.");
            }

            decimal price = excursion.PricePerPerson;
            if (price <= 0 || price > 100_000_000m || decimal.Round(price, 2) != price)
            {
                return new Failure("Для екскурсії вказана некоректна ціна.");
            }

            decimal totalPrice = price * participantsCount;
            if (totalPrice > 100_000_000m)
            {
                return new Failure("Загальна вартість бронювання надто велика.");
            }

            var reservation = new ExcursionReservation
            {
                TripId = trip.TripId,
                ExcursionSessionId = excursion.ExcursionSessionId,
                ParticipantsCount = participantsCount,
                GuestName = name,
                BookingCode = $"EX-{Guid.NewGuid():N}",
                TotalPrice = totalPrice,
                CreatedAt = DateTime.UtcNow,
                Status = "Confirmed"
            };
            bool saved = await reservations.TryReserveAsync(reservation).ConfigureAwait(false);
            if (!saved)
            {
                return new Failure("Недостатньо вільних місць або екскурсія " + "вже недоступна. Повторіть пошук.");
            }

            return Result<ExcursionReservation>.Success(reservation);
        });
    }

    public Task<Result<IReadOnlyList<ExcursionReservation>>> ListAsync(int tripId)
    {
        return operations.RunResultAsync<IReadOnlyList<ExcursionReservation>>("Excursion.ListReservations", async () =>
        {
            var check10 = session.TryRequireRole("Tourist");
            if (check10.IsFailure)
            {
                return new Failure(check10.Error);
            }

            var check11 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check11.IsFailure)
            {
                return new Failure(check11.Error);
            }

            return Result<IReadOnlyList<ExcursionReservation>>.Success(await reservations.ListForTripAsync(tripId).ConfigureAwait(false));
        });
    }

    private static Result ValidateParticipants(int count)
    {
        if (count < 1 || count > 1000)
        {
            return new Failure("Кількість учасників має бути від 1 до 1000.");
        }

        return Result.Success();
    }

    private static Result ValidateDate(DateTime date)
    {
        if (date.Date < DateTime.Today || date.Date > DateTime.Today.AddDays(59))
        {
            return new Failure("Оберіть дату в межах найближчих 60 днів.");
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
