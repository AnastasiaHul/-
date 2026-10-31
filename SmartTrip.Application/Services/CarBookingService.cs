using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class CarBookingService(
    IRepository<RentalCar> cars,
    ICarReservationRepository reservations,
    TripService trips,
    UserSession session,
    ServiceOperations operations)
{
    public Task<IReadOnlyList<RentalCar>> SearchAsync(
        string city,
        DateTime pickupDate,
        DateTime returnDate,
        int requiredSeats)
    {
        return operations.RunAsync<IReadOnlyList<RentalCar>>(
            "Car.Search",
            async () =>
            {
                session.RequireRole("Tourist");

                string selectedCity = Required(
                    city,
                    "Вкажіть місто отримання автомобіля.",
                    100);

                var dates = ValidateRental(
                    pickupDate,
                    returnDate,
                    requiredSeats);

                IReadOnlyList<RentalCar> available =
                    await cars.ListAsync(
                        x => x.IsActive
                            && x.SeatsCount >= requiredSeats)
                    .ConfigureAwait(false);

                IReadOnlyList<int> unavailableIds =
                    await reservations.ListUnavailableIdsAsync(
                        dates.PickupDate,
                        dates.ReturnDate)
                    .ConfigureAwait(false);

                HashSet<int> unavailable =
                    unavailableIds.ToHashSet();

                return available
                    .Where(x =>
                        string.Equals(
                            x.City,
                            selectedCity,
                            StringComparison.OrdinalIgnoreCase)
                        && !unavailable.Contains(x.RentalCarId))
                    .OrderBy(x => x.PricePerDay)
                    .ToList();
            });
    }

    public Task<CarReservation> ReserveAsync(
        int tripId,
        int rentalCarId,
        DateTime pickupDate,
        DateTime returnDate,
        int requiredSeats,
        string driverName,
        int driverAge)
    {
        return operations.RunAsync(
            "Car.Reserve",
            async () =>
            {
                session.RequireRole("Tourist");

                Trip trip = await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                var dates = ValidateRental(
                    pickupDate,
                    returnDate,
                    requiredSeats);

                if (dates.PickupDate.Date < trip.StartDate.Date
                    || dates.ReturnDate.Date > trip.EndDate.Date)
                {
                    throw new ArgumentException(
                        "Дати оренди мають бути в межах дат подорожі.");
                }

                string name = Required(
                    driverName,
                    "Вкажіть ім’я та прізвище водія.",
                    200);

                if (driverAge < 21 || driverAge > 100)
                {
                    throw new ArgumentException(
                        "У Smart Trip оренда доступна водіям "
                        + "віком від 21 до 100 років.");
                }

                RentalCar car =
                    await cars.FindAsync(rentalCarId)
                        .ConfigureAwait(false)
                    ?? throw new ArgumentException(
                        "Автомобіль не знайдено.");

                if (!car.IsActive)
                {
                    throw new ArgumentException(
                        "Цей автомобіль більше недоступний для оренди.");
                }

                if (requiredSeats > car.SeatsCount)
                {
                    throw new ArgumentException(
                        $"У цьому автомобілі місць: {car.SeatsCount}. "
                        + "Оберіть інший автомобіль.");
                }

                decimal dailyPrice = car.PricePerDay;

                if (dailyPrice <= 0
                    || dailyPrice > 100_000_000m
                    || decimal.Round(dailyPrice, 2) != dailyPrice)
                {
                    throw new ArgumentException(
                        "Для цього автомобіля вказана некоректна ціна.");
                }

                int days =
                    (dates.ReturnDate - dates.PickupDate).Days;

                decimal totalPrice = dailyPrice * days;

                if (totalPrice > 100_000_000m)
                {
                    throw new ArgumentException(
                        "Загальна вартість оренди надто велика.");
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

                bool saved = await reservations.TryReserveAsync(
                    reservation)
                    .ConfigureAwait(false);

                if (!saved)
                {
                    throw new ArgumentException(
                        "Цей автомобіль уже заброньовано "
                        + "на вибрані дати. Повторіть пошук.");
                }

                return reservation;
            });
    }

    public Task<IReadOnlyList<CarReservation>> ListAsync(
        int tripId)
    {
        return operations.RunAsync<IReadOnlyList<CarReservation>>(
            "Car.ListReservations",
            async () =>
            {
                session.RequireRole("Tourist");

                await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                return await reservations.ListForTripAsync(tripId)
                    .ConfigureAwait(false);
            });
    }

    private static (DateTime PickupDate, DateTime ReturnDate)
        ValidateRental(
            DateTime pickupDate,
            DateTime returnDate,
            int requiredSeats)
    {
        DateTime start = pickupDate.Date;
        DateTime end = returnDate.Date;

        if (start < DateTime.Today)
        {
            throw new ArgumentException(
                "Дата отримання авто не може бути в минулому.");
        }

        if (start > DateTime.Today.AddDays(365))
        {
            throw new ArgumentException(
                "Оренда доступна на найближчі 365 днів.");
        }

        if (end <= start)
        {
            throw new ArgumentException(
                "Дата повернення має бути пізніше "
                + "дати отримання автомобіля.");
        }

        if ((end - start).Days > 30)
        {
            throw new ArgumentException(
                "Одна оренда може тривати до 30 днів.");
        }

        if (requiredSeats < 1 || requiredSeats > 20)
        {
            throw new ArgumentException(
                "Потрібна кількість місць має бути від 1 до 20.");
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