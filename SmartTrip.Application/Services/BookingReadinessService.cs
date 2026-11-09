using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed record BookingNeeds(bool Train, bool Housing, bool Car, bool Excursion);
public sealed record ReadinessItem(string Category, bool Required, bool Ready, string Details);
public sealed record BookingReadiness(IReadOnlyList<ReadinessItem> Items)
{
    public int RequiredCount => Items.Count(item => item.Required);
    public int ReadyCount => Items.Count(item => item.Required && item.Ready);
}

public sealed class BookingReadinessService(
    TripService trips, UserSession session, IAppLogger logger,
    ITrainReservationRepository trains, IRepository<TrainDeparture> departures,
    IAccommodationReservationRepository housing, ICarReservationRepository cars,
    IExcursionReservationRepository excursions, IRepository<ExcursionSession> sessions)
{
    public async Task<Result<BookingReadiness>> CheckAsync(int tripId, BookingNeeds needs)
    {
        const string action = "Booking.ReadinessCheck";
        int? actor = session.CurrentUser?.UserId;
        try
        {
            var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (owned.IsFailure || !(needs.Train || needs.Housing || needs.Car || needs.Excursion))
            {
                string error = owned.IsFailure ? owned.Error : "Позначте хоча б один вид бронювання.";
                logger.Information($"{action}.ValidationFailed: {error}", actor);
                return Result<BookingReadiness>.Failure(error);
            }

            Trip trip = owned.Value!;
            DateTime start = trip.StartDate.Date;
            DateTime end = trip.EndDate.Date;
            if (end < start || (end - start).TotalDays > 365)
            {
                const string error = "Перевірте дати подорожі.";
                logger.Information($"{action}.ValidationFailed: {error}", actor);
                return Result<BookingReadiness>.Failure(error);
            }

            var items = new List<ReadinessItem>
            {
                needs.Train ? await CheckTrainAsync(tripId, start, end).ConfigureAwait(false)
                    : NotNeeded("Потяг"),
                needs.Housing ? await CheckHousingAsync(tripId, start, end).ConfigureAwait(false)
                    : NotNeeded("Житло"),
                needs.Car ? await CheckCarAsync(tripId, start, end).ConfigureAwait(false)
                    : NotNeeded("Авто"),
                needs.Excursion ? await CheckExcursionAsync(tripId, start, end).ConfigureAwait(false)
                    : NotNeeded("Екскурсія")
            };

            var report = new BookingReadiness(items);
            logger.Information($"{action}.Succeeded: trip {tripId}, ready {report.ReadyCount}/{report.RequiredCount}", actor);
            return report;
        }
        catch (Exception exception)
        {
            logger.Error(exception, action, actor);
            throw;
        }
    }

    private async Task<ReadinessItem> CheckTrainAsync(int tripId, DateTime start, DateTime end)
    {
        var reservations = await trains.ListForTripAsync(tripId).ConfigureAwait(false);
        int[] ids = reservations.Where(x => x.Status == "Confirmed")
            .Select(x => x.TrainDepartureId).Distinct().ToArray();
        bool ready = ids.Length != 0 && (await departures.ListAsync(x => ids.Contains(x.TrainDepartureId))
            .ConfigureAwait(false)).Any(x => x.DepartureAt.Date >= start && x.ArrivalAt.Date <= end);
        return Required("Потяг", ready, "Є підтверджений рейс у межах подорожі.",
            "Немає підтвердженого рейсу в ці дати.");
    }

    private async Task<ReadinessItem> CheckHousingAsync(int tripId, DateTime start, DateTime end)
    {
        var reservations = await housing.ListForTripAsync(tripId).ConfigureAwait(false);
        int nights = (end - start).Days;
        if (nights == 0)
        {
            return Required("Житло", true, "Подорож без ночівлі.", string.Empty);
        }

        int covered = Enumerable.Range(0, nights).Select(day => start.AddDays(day))
            .Count(night => reservations.Any(x => x.Status == "Confirmed"
                && x.CheckIn.Date <= night && x.CheckOut.Date > night));
        return Required("Житло", covered == nights, $"Покрито всі {nights} ночей.",
            $"Покрито {covered} із {nights} ночей. Бракує: {nights - covered}.");
    }

    private async Task<ReadinessItem> CheckCarAsync(int tripId, DateTime start, DateTime end)
    {
        var reservations = await cars.ListForTripAsync(tripId).ConfigureAwait(false);
        bool ready = reservations.Any(x => x.Status == "Confirmed"
            && x.PickupDate.Date <= start && x.ReturnDate.Date >= end
            && x.ReturnDate.Date > start);
        return Required("Авто", ready, "Є бронювання на весь період.",
            "Немає підтвердженого бронювання на весь період.");
    }

    private async Task<ReadinessItem> CheckExcursionAsync(int tripId, DateTime start, DateTime end)
    {
        var reservations = await excursions.ListForTripAsync(tripId).ConfigureAwait(false);
        int[] ids = reservations.Where(x => x.Status == "Confirmed")
            .Select(x => x.ExcursionSessionId).Distinct().ToArray();
        bool ready = ids.Length != 0 && (await sessions.ListAsync(x => ids.Contains(x.ExcursionSessionId))
            .ConfigureAwait(false)).Any(x => x.StartsAt.Date >= start && x.StartsAt.Date <= end);
        return Required("Екскурсія", ready, "Є підтверджена екскурсія в межах подорожі.",
            "Немає підтвердженої екскурсії в ці дати.");
    }

    private static ReadinessItem NotNeeded(string name) => new(name, false, false, "Не потрібно.");

    private static ReadinessItem Required(string name, bool ready, string yes, string no) =>
        new(name, true, ready, ready ? yes : no);
}
