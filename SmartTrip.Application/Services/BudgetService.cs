using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed record BudgetSummary(
    decimal Planned,
    decimal Spent,
    decimal Remaining,
    decimal PerPerson);

public sealed record CategoryTotal(
    string Category,
    decimal Total);

public sealed class BudgetService(
    IRepository<Expense> expenses,
    IRepository<Place> places,
    TripService trips,
    ServiceOperations operations)
{
    public Task AddPlaceExpenseAsync(
        int tripId,
        int placeId,
        DateTime date)
    {
        return operations.RunAsync(
            "AddPlaceToExpenses",
            async () =>
            {
                // Перевіряємо, що подорож належить туристу.
                await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                // Отримуємо місце та його актуальну ціну з бази.
                Place place = await places.FindAsync(placeId)
                    .ConfigureAwait(false)
                    ?? throw new ArgumentException(
                        "Місце не знайдено.");

                if (place.Status == "Hidden")
                {
                    throw new ArgumentException(
                        "Це місце зараз недоступне.");
                }

                if (place.Price <= 0)
                {
                    throw new ArgumentException(
                        "Відвідування безкоштовне — "
                        + "додавати витрату не потрібно.");
                }

                // Зберігаємо витрату за одне відвідування.
                await AddExpenseAsync(
                    tripId,
                    place.Category,
                    $"Відвідування: {place.Name}",
                    place.Price,
                    date)
                    .ConfigureAwait(false);
            });
    }

    public Task<BudgetSummary> CalculateAsync(int tripId)
    {
        return operations.RunAsync(
            "CalculateBudget",
            async () =>
            {
                Trip trip = await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                IReadOnlyList<Expense> items =
                    await expenses.ListAsync(
                        x => x.TripId == tripId)
                    .ConfigureAwait(false);

                decimal spent = items.Sum(x => x.Amount);

                return new BudgetSummary(
                    trip.Budget,
                    spent,
                    trip.Budget - spent,
                    trip.PeopleCount > 0
                        ? decimal.Round(
                            spent / trip.PeopleCount, 2)
                        : 0);
            });
    }

    public Task AddExpenseAsync(
        int tripId,
        string category,
        string description,
        decimal amount,
        DateTime date)
    {
        return operations.RunAsync("AddExpense", async () =>
        {
            Trip trip = await trips.OwnedAsync(tripId)
                .ConfigureAwait(false);

            string validCategory =
                ServiceOperations.Required(
                    category, "Категорія", 100);

            string validDescription =
                ServiceOperations.Required(
                    description, "Опис витрати", 300);

            decimal validAmount =
                ServiceOperations.Money(amount, false);

            if (date.Date < trip.StartDate.Date
                || date.Date > trip.EndDate.Date)
            {
                throw new ArgumentException(
                    "Дата витрати повинна бути в межах подорожі.");
            }

            var expense = new Expense
            {
                TripId = tripId,
                Category = validCategory,
                Description = validDescription,
                Amount = validAmount,
                Date = DateTime.SpecifyKind(
                    date.Date, DateTimeKind.Utc)
            };

            await expenses.AddAsync(expense)
                .ConfigureAwait(false);
        });
    }

    public Task<IReadOnlyList<Expense>> ListAsync(int tripId)
    {
        return operations.RunAsync("ListExpenses", async () =>
        {
            await trips.OwnedAsync(tripId)
                .ConfigureAwait(false);

            return await expenses.ListAsync(
                x => x.TripId == tripId)
                .ConfigureAwait(false);
        });
    }

    public Task<IReadOnlyList<CategoryTotal>> StatisticsAsync(
        int tripId)
    {
        return operations.RunAsync<IReadOnlyList<CategoryTotal>>(
            "ViewExpenseStatistics",
            async () =>
            {
                await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                IReadOnlyList<Expense> items =
                    await expenses.ListAsync(
                        x => x.TripId == tripId)
                    .ConfigureAwait(false);

                return items
                    .GroupBy(
                        x => x.Category,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => new CategoryTotal(
                        group.Key,
                        group.Sum(x => x.Amount)))
                    .OrderByDescending(x => x.Total)
                    .ToList();
            });
    }
}