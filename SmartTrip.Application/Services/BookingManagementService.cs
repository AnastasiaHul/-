using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed record TripBooking(
    BookingKind Kind,
    int ReservationId,
    string Code,
    string Title,
    DateTime StartsAt,
    decimal Price,
    bool IsConfirmed,
    bool CanCancel,
    bool InExpenses)
{
    public string KindName => Kind switch
    {
        BookingKind.Train => "Потяг",
        BookingKind.Housing => "Житло",
        BookingKind.Car => "Авто",
        _ => "Екскурсія"
    };

    public string StatusName => IsConfirmed ? "Підтверджено" : "Скасовано";

    public string ExpenseState => InExpenses ? "Так" : "Ні";
}

public sealed class BookingManagementService(
    TripService trips,
    ServiceOperations operations,
    ITrainReservationRepository trains,
    IAccommodationReservationRepository housing,
    ICarReservationRepository cars,
    IExcursionReservationRepository excursions,
    IRepository<TrainDeparture> departures,
    IRepository<Accommodation> accommodations,
    IRepository<RentalCar> rentalCars,
    IRepository<ExcursionSession> sessions,
    IRepository<Expense> expenses,
    IBookingCancellationRepository cancellations)
{
    // Позначка в описі витрати, за якою вона пов’язана з бронюванням.
    internal static string Marker(string code) => $"[{code}]";

    public Task<Result<IReadOnlyList<TripBooking>>> ListAsync(int tripId)
    {
        return operations.RunResultAsync<IReadOnlyList<TripBooking>>("Booking.ListAll", async () =>
        {
            var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (owned.IsFailure)
            {
                return new Failure(owned.Error);
            }

            return Result<IReadOnlyList<TripBooking>>.Success(await LoadAsync(tripId).ConfigureAwait(false));
        });
    }

    public Task<Result> CancelAsync(int tripId, BookingKind kind, int reservationId)
    {
        return operations.RunResultAsync("Booking.CancelOwn", async () =>
        {
            var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (owned.IsFailure)
            {
                return new Failure(owned.Error);
            }

            IReadOnlyList<TripBooking> all = await LoadAsync(tripId).ConfigureAwait(false);
            TripBooking? booking = all.FirstOrDefault(x => x.Kind == kind && x.ReservationId == reservationId);
            if (booking is null)
            {
                return new Failure("Бронювання не знайдено у цій подорожі.");
            }

            if (!booking.IsConfirmed)
            {
                return new Failure("Це бронювання вже скасоване.");
            }

            if (!booking.CanCancel)
            {
                return new Failure("Скасувати можна лише бронювання, яке ще не почалося. "
                    + "Житло та авто — не пізніше ніж за день до початку.");
            }

            bool cancelled = await cancellations
                .TryCancelAsync(kind, reservationId, tripId, Marker(booking.Code))
                .ConfigureAwait(false);
            return cancelled
                ? Result.Success()
                : Result.Failure("Стан бронювання вже змінився. Оновіть список.");
        });
    }

    // Повертає кількість нових витрат. Повторний виклик не створює дублікатів.
    public Task<Result<int>> AddToExpensesAsync(int tripId)
    {
        return operations.RunResultAsync<int>("Booking.AddToExpenses", async () =>
        {
            var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (owned.IsFailure)
            {
                return new Failure(owned.Error);
            }

            Trip trip = owned.Value!;
            IReadOnlyList<TripBooking> all = await LoadAsync(tripId).ConfigureAwait(false);
            int added = 0;
            foreach (TripBooking booking in all.Where(x => x.IsConfirmed && !x.InExpenses && x.Price > 0))
            {
                // Дата витрати завжди залишається в межах подорожі.
                DateTime date = booking.StartsAt.Date;
                if (date < trip.StartDate.Date)
                {
                    date = trip.StartDate.Date;
                }

                if (date > trip.EndDate.Date)
                {
                    date = trip.EndDate.Date;
                }

                string title = booking.Title.Length > 220 ? booking.Title[..220] : booking.Title;
                await expenses.AddAsync(new Expense
                {
                    TripId = tripId,
                    Category = booking.Kind switch
                    {
                        BookingKind.Train => "Транспорт",
                        BookingKind.Housing => "Житло",
                        BookingKind.Car => "Оренда авто",
                        _ => "Екскурсії"
                    },
                    Description = $"Бронювання: {title} {Marker(booking.Code)}",
                    Amount = booking.Price,
                    Date = DateTime.SpecifyKind(date, DateTimeKind.Utc)
                }).ConfigureAwait(false);
                added++;
            }

            return added;
        });
    }

    private async Task<IReadOnlyList<TripBooking>> LoadAsync(int tripId)
    {
        IReadOnlyList<Expense> spent = await expenses.ListAsync(x => x.TripId == tripId).ConfigureAwait(false);
        // Час у розкладах зберігається як місцевий.
        DateTime now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
        var result = new List<TripBooking>();

        TripBooking Create(BookingKind kind, int id, string code, string title,
            DateTime startsAt, decimal price, string status, bool notStarted)
        {
            bool confirmed = status == "Confirmed";
            bool inExpenses = spent.Any(x => x.Description.EndsWith(Marker(code), StringComparison.Ordinal));
            return new TripBooking(kind, id, code, title, startsAt, price,
                confirmed, confirmed && notStarted, inExpenses);
        }

        foreach (TrainReservation item in await trains.ListForTripAsync(tripId).ConfigureAwait(false))
        {
            TrainDeparture? departure = await departures.FindAsync(item.TrainDepartureId).ConfigureAwait(false);
            DateTime startsAt = departure?.DepartureAt ?? item.CreatedAt;
            string title = departure is null
                ? "Квиток на потяг"
                : $"Потяг {departure.TrainNumber}, {departure.DepartureCity} — {departure.ArrivalCity}";
            title += $", вагон {item.WagonNumber}, місце {item.SeatNumber}";
            result.Add(Create(BookingKind.Train, item.TrainReservationId, item.BookingCode, title,
                startsAt, item.TotalPrice, item.Status, departure is not null && startsAt > now));
        }

        foreach (AccommodationReservation item in await housing.ListForTripAsync(tripId).ConfigureAwait(false))
        {
            Accommodation? place = await accommodations.FindAsync(item.AccommodationId).ConfigureAwait(false);
            result.Add(Create(BookingKind.Housing, item.AccommodationReservationId, item.BookingCode,
                place?.Name ?? "Проживання", item.CheckIn, item.TotalPrice, item.Status,
                item.CheckIn.Date > now.Date));
        }

        foreach (CarReservation item in await cars.ListForTripAsync(tripId).ConfigureAwait(false))
        {
            RentalCar? car = await rentalCars.FindAsync(item.RentalCarId).ConfigureAwait(false);
            result.Add(Create(BookingKind.Car, item.CarReservationId, item.BookingCode,
                car is null ? "Оренда авто" : $"{car.Brand} {car.Model}", item.PickupDate,
                item.TotalPrice, item.Status, item.PickupDate.Date > now.Date));
        }

        foreach (ExcursionReservation item in await excursions.ListForTripAsync(tripId).ConfigureAwait(false))
        {
            ExcursionSession? session = await sessions.FindAsync(item.ExcursionSessionId).ConfigureAwait(false);
            DateTime startsAt = session?.StartsAt ?? item.CreatedAt;
            result.Add(Create(BookingKind.Excursion, item.ExcursionReservationId, item.BookingCode,
                session?.Title ?? "Екскурсія", startsAt, item.TotalPrice, item.Status,
                session is not null && startsAt > now));
        }

        return result.OrderBy(x => x.StartsAt).ToList();
    }
}
