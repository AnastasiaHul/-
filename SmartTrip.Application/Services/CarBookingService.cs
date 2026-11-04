using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace SmartTrip.Application.Services;
public sealed class CarBookingService(IRepository<RentalCar> cars, ICarReservationRepository reservations, TripService trips, UserSession session, ServiceOperations operations)
{
    public Task<Result<IReadOnlyList<RentalCar>>> SearchAsync(string city, DateTime pickupDate, DateTime returnDate, int requiredSeats)
    {
        return operations.RunResultAsync<IReadOnlyList<RentalCar>>("Car.Search", async () =>
        {
            var check1 = session.TryRequireRole("Tourist");
            if (check1.IsFailure)
            {
                return new Failure(check1.Error);
            }

            var check2 = Required(city, "Вкажіть місто отримання автомобіля.", 100);
            if (check2.IsFailure)
            {
                return new Failure(check2.Error);
            }

            string selectedCity = check2.Value!;
            var check3 = ValidateRental(pickupDate, returnDate, requiredSeats);
            if (check3.IsFailure)
            {
                return new Failure(check3.Error);
            }

            var dates = check3.Value!;
            IReadOnlyList<RentalCar> available = await cars.ListAsync(x => x.IsActive && x.SeatsCount >= requiredSeats).ConfigureAwait(false);
            IReadOnlyList<int> unavailableIds = await reservations.ListUnavailableIdsAsync(dates.PickupDate, dates.ReturnDate).ConfigureAwait(false);
            HashSet<int> unavailable = unavailableIds.ToHashSet();
            return Result<IReadOnlyList<RentalCar>>.Success(available.Where(x => string.Equals(x.City, selectedCity, StringComparison.OrdinalIgnoreCase) && !unavailable.Contains(x.RentalCarId)).OrderBy(x => x.PricePerDay).ToList());
        });
    }

    public Task<Result<CarReservation>> ReserveAsync(int tripId, int rentalCarId, DateTime pickupDate, DateTime returnDate, int requiredSeats, string driverName, int driverAge)
    {
        return operations.RunResultAsync<CarReservation>("Car.Reserve", async () =>
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
            var check6 = ValidateRental(pickupDate, returnDate, requiredSeats);
            if (check6.IsFailure)
            {
                return new Failure(check6.Error);
            }

            var dates = check6.Value!;
            if (dates.PickupDate.Date < trip.StartDate.Date || dates.ReturnDate.Date > trip.EndDate.Date)
            {
                return new Failure("Дати оренди мають бути в межах дат подорожі.");
            }

            var check7 = Required(driverName, "Вкажіть ім’я та прізвище водія.", 200);
            if (check7.IsFailure)
            {
                return new Failure(check7.Error);
            }

            string name = check7.Value!;
            if (driverAge < 21 || driverAge > 100)
            {
                return new Failure("У Smart Trip оренда доступна водіям " + "віком від 21 до 100 років.");
            }

            RentalCar? car = await cars.FindAsync(rentalCarId).ConfigureAwait(false);
            if (car is null)
            {
                return new Failure("Автомобіль не знайдено.");
            }

            if (!car.IsActive)
            {
                return new Failure("Цей автомобіль більше недоступний для оренди.");
            }

            if (requiredSeats > car.SeatsCount)
            {
                return new Failure($"У цьому автомобілі місць: {car.SeatsCount}. " + "Оберіть інший автомобіль.");
            }

            decimal dailyPrice = car.PricePerDay;
            if (dailyPrice <= 0 || dailyPrice > 100_000_000m || decimal.Round(dailyPrice, 2) != dailyPrice)
            {
                return new Failure("Для цього автомобіля вказана некоректна ціна.");
            }

            int days = (dates.ReturnDate - dates.PickupDate).Days;
            decimal totalPrice = dailyPrice * days;
            if (totalPrice > 100_000_000m)
            {
                return new Failure("Загальна вартість оренди надто велика.");
            }

            var reservation = new CarReservation
            {
                TripId = trip.TripId,
                RentalCarId = car.RentalCarId,
                PickupDate = dates.PickupDate,
                ReturnDate = dates.ReturnDate,
                DriverName = name,
                DriverAge = driverAge,
                BookingCode = $"CR-{Guid.NewGuid():N}",
                TotalPrice = totalPrice,
                CreatedAt = DateTime.UtcNow,
                Status = "Confirmed"
            };
            bool saved = await reservations.TryReserveAsync(reservation).ConfigureAwait(false);
            if (!saved)
            {
                return new Failure("Цей автомобіль уже заброньовано " + "на вибрані дати. Повторіть пошук.");
            }

            return Result<CarReservation>.Success(reservation);
        });
    }

    public Task<Result<IReadOnlyList<CarReservation>>> ListAsync(int tripId)
    {
        return operations.RunResultAsync<IReadOnlyList<CarReservation>>("Car.ListReservations", async () =>
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

            return Result<IReadOnlyList<CarReservation>>.Success(await reservations.ListForTripAsync(tripId).ConfigureAwait(false));
        });
    }

    private static Result<(DateTime PickupDate, DateTime ReturnDate)> ValidateRental(DateTime pickupDate, DateTime returnDate, int requiredSeats)
    {
        DateTime start = pickupDate.Date;
        DateTime end = returnDate.Date;
        if (start < DateTime.Today)
        {
            return new Failure("Дата отримання авто не може бути в минулому.");
        }

        if (start > DateTime.Today.AddDays(365))
        {
            return new Failure("Оренда доступна на найближчі 365 днів.");
        }

        if (end <= start)
        {
            return new Failure("Дата повернення має бути пізніше " + "дати отримання автомобіля.");
        }

        if ((end - start).Days > 30)
        {
            return new Failure("Одна оренда може тривати до 30 днів.");
        }

        if (requiredSeats < 1 || requiredSeats > 20)
        {
            return new Failure("Потрібна кількість місць має бути від 1 до 20.");
        }

        return Result<(DateTime PickupDate, DateTime ReturnDate)>.Success((DateTime.SpecifyKind(start, DateTimeKind.Utc), DateTime.SpecifyKind(end, DateTimeKind.Utc)));
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
