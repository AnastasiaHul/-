using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace SmartTrip.Application.Services;
public sealed class AccommodationBookingService(IRepository<Accommodation> accommodations, IAccommodationReservationRepository reservations, TripService trips, UserSession session, ServiceOperations operations)
{
    public Task<Result<IReadOnlyList<Accommodation>>> SearchAsync(string city, DateTime checkIn, DateTime checkOut, int guestsCount)
    {
        return operations.RunResultAsync<IReadOnlyList<Accommodation>>("Accommodation.Search", async () =>
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
            var check3 = ValidateStay(checkIn, checkOut, guestsCount);
            if (check3.IsFailure)
            {
                return new Failure(check3.Error);
            }

            var dates = check3.Value!;
            IReadOnlyList<Accommodation> available = await accommodations.ListAsync(x => x.IsActive && x.MaxGuests >= guestsCount).ConfigureAwait(false);
            IReadOnlyList<int> unavailableIds = await reservations.ListUnavailableIdsAsync(dates.CheckIn, dates.CheckOut).ConfigureAwait(false);
            HashSet<int> unavailable = unavailableIds.ToHashSet();
            return Result<IReadOnlyList<Accommodation>>.Success(available.Where(x => string.Equals(x.City, selectedCity, StringComparison.OrdinalIgnoreCase) && !unavailable.Contains(x.AccommodationId)).OrderBy(x => x.PricePerNight).ToList());
        });
    }

    public Task<Result<AccommodationReservation>> ReserveAsync(int tripId, int accommodationId, DateTime checkIn, DateTime checkOut, int guestsCount, string guestName)
    {
        return operations.RunResultAsync<AccommodationReservation>("Accommodation.Reserve", async () =>
        {
            var check4 = session.TryRequireRole("Tourist");
            if (check4.IsFailure)
            {
                return new Failure(check4.Error);
            }

            var check5 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check5.IsFailure)
            {
                return new Failure(check5.Error);
            }

            Trip trip = check5.Value!;
            var check6 = ValidateStay(checkIn, checkOut, guestsCount);
            if (check6.IsFailure)
            {
                return new Failure(check6.Error);
            }

            var dates = check6.Value!;
            if (dates.CheckIn.Date < trip.StartDate.Date || dates.CheckOut.Date > trip.EndDate.Date)
            {
                return new Failure("Дати проживання мають бути в межах дат подорожі.");
            }

            var check7 = Required(guestName, "Вкажіть ім’я та прізвище гостя.", 200);
            if (check7.IsFailure)
            {
                return new Failure(check7.Error);
            }

            string name = check7.Value!;
            Accommodation? accommodation = await accommodations.FindAsync(accommodationId).ConfigureAwait(false);
            if (accommodation is null)
            {
                return new Failure("Житло не знайдено.");
            }

            if (!accommodation.IsActive)
            {
                return new Failure("Це житло більше недоступне для бронювання.");
            }

            if (guestsCount > accommodation.MaxGuests)
            {
                return new Failure($"Максимальна кількість гостей: " + $"{accommodation.MaxGuests}.");
            }

            decimal nightlyPrice = accommodation.PricePerNight;
            if (nightlyPrice <= 0 || nightlyPrice > 100_000_000m || decimal.Round(nightlyPrice, 2) != nightlyPrice)
            {
                return new Failure("Для цього житла вказана некоректна ціна.");
            }

            int nights = (dates.CheckOut - dates.CheckIn).Days;
            decimal totalPrice = nightlyPrice * nights;
            if (totalPrice > 100_000_000m)
            {
                return new Failure("Загальна вартість бронювання надто велика.");
            }

            var reservation = new AccommodationReservation
            {
                TripId = trip.TripId,
                AccommodationId = accommodation.AccommodationId,
                CheckIn = dates.CheckIn,
                CheckOut = dates.CheckOut,
                GuestsCount = guestsCount,
                GuestName = name,
                BookingCode = $"HT-{Guid.NewGuid():N}",
                TotalPrice = totalPrice,
                CreatedAt = DateTime.UtcNow,
                Status = "Confirmed"
            };
            bool saved = await reservations.TryReserveAsync(reservation).ConfigureAwait(false);
            if (!saved)
            {
                return new Failure("Це житло вже заброньовано на вибрані дати. " + "Повторіть пошук або змініть дати.");
            }

            return Result<AccommodationReservation>.Success(reservation);
        });
    }

    public Task<Result<IReadOnlyList<AccommodationReservation>>> ListAsync(int tripId)
    {
        return operations.RunResultAsync<IReadOnlyList<AccommodationReservation>>("Accommodation.ListReservations", async () =>
        {
            var check8 = session.TryRequireRole("Tourist");
            if (check8.IsFailure)
            {
                return new Failure(check8.Error);
            }

            var check9 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check9.IsFailure)
            {
                return new Failure(check9.Error);
            }

            return Result<IReadOnlyList<AccommodationReservation>>.Success(await reservations.ListForTripAsync(tripId).ConfigureAwait(false));
        });
    }

    private static Result<(DateTime CheckIn, DateTime CheckOut)> ValidateStay(DateTime checkIn, DateTime checkOut, int guestsCount)
    {
        DateTime start = checkIn.Date;
        DateTime end = checkOut.Date;
        if (start < DateTime.Today)
        {
            return new Failure("Дата заїзду не може бути в минулому.");
        }

        if (start > DateTime.Today.AddDays(365))
        {
            return new Failure("Бронювання доступне на найближчі 365 днів.");
        }

        if (end <= start)
        {
            return new Failure("Дата виїзду має бути пізніше дати заїзду.");
        }

        if ((end - start).Days > 30)
        {
            return new Failure("Одне бронювання може тривати до 30 ночей.");
        }

        if (guestsCount < 1 || guestsCount > 20)
        {
            return new Failure("Кількість гостей має бути від 1 до 20.");
        }

        return Result<(DateTime CheckIn, DateTime CheckOut)>.Success((DateTime.SpecifyKind(start, DateTimeKind.Utc), DateTime.SpecifyKind(end, DateTimeKind.Utc)));
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
}
