using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class TripService(
    IRepository<Trip> trips,
    UserSession session,
    ServiceOperations operations)
{
    public Task<IReadOnlyList<Trip>> ListAsync()
    {
        return operations.RunAsync("ListTrips", () =>
        {
            int userId = session.RequireRole("Tourist").UserId;

            return trips.ListAsync(x => x.UserId == userId);
        });
    }

    public Task<Trip> SaveAsync(
        int? id,
        string title,
        string city,
        DateTime start,
        DateTime end,
        decimal budget,
        int people,
        string interests)
    {
        return operations.RunAsync(
            id is null ? "CreateTrip" : "EditTrip",
            async () =>
            {
                int userId =
                    session.RequireRole("Tourist").UserId;

                string validTitle =
                    ServiceOperations.Required(title, "Назва", 150);

                string validCity =
                    ServiceOperations.Required(city, "Місто", 100);

                if (start == default
                    || end < start
                    || (end - start).TotalDays > 365)
                {
                    throw new ArgumentException(
                        "Кінець подорожі не може бути раніше початку. "
                        + "Тривалість — максимум 365 днів.");
                }

                if (people < 1 || people > 1000)
                {
                    throw new ArgumentException(
                        "Кількість людей: від 1 до 1000.");
                }

                if (interests.Length > 1000)
                {
                    throw new ArgumentException(
                        "Інтереси: максимум 1000 символів.");
                }

                decimal validBudget =
                    ServiceOperations.Money(budget);

                Trip trip = id is null
                    ? new Trip { UserId = userId }
                    : await OwnedAsync(id.Value)
                        .ConfigureAwait(false);

                trip.Title = validTitle;
                trip.City = validCity;

                trip.StartDate = DateTime.SpecifyKind(
                    start.Date, DateTimeKind.Utc);

                trip.EndDate = DateTime.SpecifyKind(
                    end.Date, DateTimeKind.Utc);

                trip.Budget = validBudget;
                trip.PeopleCount = people;
                trip.Interests = interests.Trim();

                if (id is null)
                {
                    await trips.AddAsync(trip)
                        .ConfigureAwait(false);
                }
                else
                {
                    await trips.UpdateAsync(trip)
                        .ConfigureAwait(false);
                }

                return trip;
            });
    }

    public Task DeleteAsync(int id)
    {
        return operations.RunAsync("DeleteTrip", async () =>
        {
            Trip trip = await OwnedAsync(id)
                .ConfigureAwait(false);

            await trips.DeleteAsync(trip)
                .ConfigureAwait(false);
        });
    }

    internal async Task<Trip> OwnedAsync(int id)
    {
        int userId = session.RequireRole("Tourist").UserId;

        Trip trip = await trips.FindAsync(id)
            .ConfigureAwait(false)
            ?? throw new ArgumentException(
                "Подорож не знайдена.");

        if (trip.UserId != userId)
        {
            throw new UnauthorizedAccessException(
                "Ви можете керувати лише власними подорожами.");
        }

        return trip;
    }
}