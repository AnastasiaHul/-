using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace SmartTrip.Application.Services;
public sealed record BudgetSummary(decimal Planned, decimal Spent, decimal Remaining, decimal PerPerson);
public sealed record CategoryTotal(string Category, decimal Total);
public sealed class BudgetService(IRepository<Expense> expenses, IRepository<Place> places, TripService trips, ServiceOperations operations)
{
    public Task<Result> AddPlaceExpenseAsync(int tripId, int placeId, DateTime date)
    {
        return operations.RunResultAsync("AddPlaceToExpenses", async () =>
        {
            var check1 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check1.IsFailure)
            {
                return new Failure(check1.Error);
            }

            // Отримуємо місце та його актуальну ціну з бази.
            Place? place = await places.FindAsync(placeId).ConfigureAwait(false);
            if (place is null)
            {
                return new Failure("Місце не знайдено.");
            }

            if (place.Status == "Hidden")
            {
                return new Failure("Це місце зараз недоступне.");
            }

            if (place.Price <= 0)
            {
                return new Failure("Відвідування безкоштовне — " + "додавати витрату не потрібно.");
            }

            var check2 = await AddExpenseAsync(tripId, place.Category, $"Відвідування: {place.Name}", place.Price, date).ConfigureAwait(false);
            if (check2.IsFailure)
            {
                return new Failure(check2.Error);
            }

            return Result.Success();
        });
    }

    public Task<Result<BudgetSummary>> CalculateAsync(int tripId)
    {
        return operations.RunResultAsync<BudgetSummary>("CalculateBudget", async () =>
        {
            var check3 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check3.IsFailure)
            {
                return new Failure(check3.Error);
            }

            Trip trip = check3.Value!;
            IReadOnlyList<Expense> items = await expenses.ListAsync(x => x.TripId == tripId).ConfigureAwait(false);
            decimal spent = items.Sum(x => x.Amount);
            return Result<BudgetSummary>.Success(new BudgetSummary(trip.Budget, spent, trip.Budget - spent, trip.PeopleCount > 0 ? decimal.Round(spent / trip.PeopleCount, 2) : 0));
        });
    }

    public Task<Result> AddExpenseAsync(int tripId, string category, string description, decimal amount, DateTime date)
    {
        return operations.RunResultAsync("AddExpense", async () =>
        {
            var check4 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check4.IsFailure)
            {
                return new Failure(check4.Error);
            }

            Trip trip = check4.Value!;
            var check5 = ServiceOperations.TryRequired(category, "Категорія", 100);
            if (check5.IsFailure)
            {
                return new Failure(check5.Error);
            }

            string validCategory = check5.Value!;
            var check6 = ServiceOperations.TryRequired(description, "Опис витрати", 300);
            if (check6.IsFailure)
            {
                return new Failure(check6.Error);
            }

            string validDescription = check6.Value!;
            var check7 = ServiceOperations.TryMoney(amount, false);
            if (check7.IsFailure)
            {
                return new Failure(check7.Error);
            }

            decimal validAmount = check7.Value!;
            if (date.Date < trip.StartDate.Date || date.Date > trip.EndDate.Date)
            {
                return new Failure("Дата витрати повинна бути в межах подорожі.");
            }

            var expense = new Expense
            {
                TripId = tripId,
                Category = validCategory,
                Description = validDescription,
                Amount = validAmount,
                Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc)
            };
            await expenses.AddAsync(expense).ConfigureAwait(false);
            return Result.Success();
        });
    }

    public Task<Result<IReadOnlyList<Expense>>> ListAsync(int tripId)
    {
        return operations.RunResultAsync<IReadOnlyList<Expense>>("ListExpenses", async () =>
        {
            var check8 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check8.IsFailure)
            {
                return new Failure(check8.Error);
            }

            return Result<IReadOnlyList<Expense>>.Success(await expenses.ListAsync(x => x.TripId == tripId).ConfigureAwait(false));
        });
    }

    public Task<Result<IReadOnlyList<CategoryTotal>>> StatisticsAsync(int tripId)
    {
        return operations.RunResultAsync<IReadOnlyList<CategoryTotal>>("ViewExpenseStatistics", async () =>
        {
            var check9 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check9.IsFailure)
            {
                return new Failure(check9.Error);
            }

            IReadOnlyList<Expense> items = await expenses.ListAsync(x => x.TripId == tripId).ConfigureAwait(false);
            return Result<IReadOnlyList<CategoryTotal>>.Success(items.GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase).Select(group => new CategoryTotal(group.Key, group.Sum(x => x.Amount))).OrderByDescending(x => x.Total).ToList());
        });
    }
}
