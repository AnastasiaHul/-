using System.Text.RegularExpressions;
using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class ExpenseEditingService(
    IRepository<Expense> expenses,
    TripService trips,
    ServiceOperations operations)
{
    public Task<Result> UpdateAsync(int expenseId, string category, string description,
        decimal amount, DateTime date)
    {
        return operations.RunResultAsync("Expense.Edit", async () =>
        {
            var found = await OwnedAsync(expenseId).ConfigureAwait(false);
            if (found.IsFailure)
            {
                return new Failure(found.Error);
            }

            (Expense expense, Trip trip) = found.Value;
            if (IsBookingExpense(expense.Description))
            {
                return new Failure("Витрату з бронювання не можна редагувати. "
                    + "Її можна видалити або скасувати саме бронювання.");
            }

            var validCategory = ServiceOperations.TryRequired(category, "Категорія", 100);
            if (validCategory.IsFailure)
            {
                return new Failure(validCategory.Error);
            }

            var validDescription = ServiceOperations.TryRequired(description, "Опис витрати", 300);
            if (validDescription.IsFailure)
            {
                return new Failure(validDescription.Error);
            }

            if (IsBookingExpense(validDescription.Value!))
            {
                return new Failure("Опис не може закінчуватися кодом бронювання.");
            }

            var validAmount = ServiceOperations.TryMoney(amount, false);
            if (validAmount.IsFailure)
            {
                return new Failure(validAmount.Error);
            }

            if (date.Date < trip.StartDate.Date || date.Date > trip.EndDate.Date)
            {
                return new Failure("Дата витрати повинна бути в межах подорожі.");
            }

            expense.Category = validCategory.Value!;
            expense.Description = validDescription.Value!;
            expense.Amount = validAmount.Value;
            expense.Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            await expenses.UpdateAsync(expense).ConfigureAwait(false);
            return Result.Success();
        });
    }

    public Task<Result> DeleteAsync(int expenseId)
    {
        return operations.RunResultAsync("Expense.Delete", async () =>
        {
            var found = await OwnedAsync(expenseId).ConfigureAwait(false);
            if (found.IsFailure)
            {
                return new Failure(found.Error);
            }

            await expenses.DeleteAsync(found.Value.Expense).ConfigureAwait(false);
            return Result.Success();
        });
    }

    private static bool IsBookingExpense(string description) =>
        Regex.IsMatch(description, @"\[(ST|HT|CR|EX)-[0-9a-fA-F]{32}\]$",
            RegexOptions.None, TimeSpan.FromSeconds(1));

    private async Task<Result<(Expense Expense, Trip Trip)>> OwnedAsync(int expenseId)
    {
        Expense? expense = await expenses.FindAsync(expenseId).ConfigureAwait(false);
        if (expense is null)
        {
            return new Failure("Витрату не знайдено.");
        }

        var owned = await trips.OwnedAsync(expense.TripId).ConfigureAwait(false);
        if (owned.IsFailure)
        {
            return new Failure(owned.Error);
        }

        return (expense, owned.Value!);
    }
}
