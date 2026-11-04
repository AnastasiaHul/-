using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class TripService(
    IRepository<Trip> trips,
    UserSession session,
    ServiceOperations operations)
{
    public Task<Result<IReadOnlyList<Trip>>> ListAsync()
    {
        return operations.RunResultAsync<IReadOnlyList<Trip>>(
            "ListTrips",
            async () =>
            {
                Result<User> access =
                    session.TryRequireRole("Tourist");

                if (access.IsFailure)
                {
                    return Result<IReadOnlyList<Trip>>.Failure(
                        access.Error);
                }

                int userId = access.Value!.UserId;

                IReadOnlyList<Trip> result =
                    await trips.ListAsync(x => x.UserId == userId)
                        .ConfigureAwait(false);

                return Result<IReadOnlyList<Trip>>.Success(result);
            });
    }

    public Task<Result<Trip>> SaveAsync(
        int? id,
        string? title,
        string? city,
        DateTime start,
        DateTime end,
        decimal budget,
        int people,
        string? interests)
    {
        return operations.RunResultAsync<Trip>(
            id is null ? "CreateTrip" : "EditTrip",
            async () =>
            {
                Result<User> access =
                    session.TryRequireRole("Tourist");

                if (access.IsFailure)
                {
                    return Result<Trip>.Failure(access.Error);
                }

                Result<string> validTitle =
                    ServiceOperations.TryRequired(
                        title,
                        "Назва",
                        150);

                if (validTitle.IsFailure)
                {
                    return Result<Trip>.Failure(validTitle.Error);
                }

                Result<string> validCity =
                    ServiceOperations.TryRequired(
                        city,
                        "Місто",
                        100);

                if (validCity.IsFailure)
                {
                    return Result<Trip>.Failure(validCity.Error);
                }

                if (start == default
                    || end < start
                    || (end - start).TotalDays > 365)
                {
                    return Result<Trip>.Failure(
                        "Кінець подорожі не може бути раніше початку. "
                        + "Тривалість — максимум 365 днів.");
                }

                if (people < 1 || people > 1000)
                {
                    return Result<Trip>.Failure(
                        "Кількість людей: від 1 до 1000.");
                }

                string interestsText = interests ?? string.Empty;

                if (interestsText.Length > 1000)
                {
                    return Result<Trip>.Failure(
                        "Інтереси: максимум 1000 символів.");
                }

                Result<decimal> validBudget =
                    ServiceOperations.TryMoney(budget);

                if (validBudget.IsFailure)
                {
                    return Result<Trip>.Failure(validBudget.Error);
                }

                Trip trip;

                if (id is null)
                {
                    trip = new Trip
                    {
                        UserId = access.Value!.UserId
                    };
                }
                else
                {
                    Result<Trip> owned =
                        await OwnedAsync(id.Value)
                            .ConfigureAwait(false);

                    if (owned.IsFailure)
                    {
                        return Result<Trip>.Failure(owned.Error);
                    }

                    trip = owned.Value!;
                }

                trip.Title = validTitle.Value!;
                trip.City = validCity.Value!;

                trip.StartDate = DateTime.SpecifyKind(
                    start.Date,
                    DateTimeKind.Utc);

                trip.EndDate = DateTime.SpecifyKind(
                    end.Date,
                    DateTimeKind.Utc);

                trip.Budget = validBudget.Value;
                trip.PeopleCount = people;
                trip.Interests = interestsText.Trim();

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

                return Result<Trip>.Success(trip);
            });
    }

    public Task<Result> DeleteAsync(int id)
    {
        return operations.RunResultAsync(
            "DeleteTrip",
            async () =>
            {
                Result<Trip> owned =
                    await OwnedAsync(id)
                        .ConfigureAwait(false);

                if (owned.IsFailure)
                {
                    return Result.Failure(owned.Error);
                }

                await trips.DeleteAsync(owned.Value!)
                    .ConfigureAwait(false);

                return Result.Success();
            });
    }

    internal async Task<Result<Trip>> OwnedAsync(int id)
    {
        Result<User> access =
            session.TryRequireRole("Tourist");

        if (access.IsFailure)
        {
            return Result<Trip>.Failure(access.Error);
        }

        if (id <= 0)
        {
            return Result<Trip>.Failure(
                "Некоректний ідентифікатор подорожі.");
        }

        Trip? trip = await trips.FindAsync(id)
            .ConfigureAwait(false);

        if (trip is null)
        {
            return Result<Trip>.Failure(
                "Подорож не знайдена.");
        }

        if (trip.UserId != access.Value!.UserId)
        {
            return Result<Trip>.Failure(
                "Ви можете керувати лише власними подорожами.");
        }

        return Result<Trip>.Success(trip);
    }

}
