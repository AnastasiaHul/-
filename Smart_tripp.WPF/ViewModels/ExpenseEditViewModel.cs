using System.Collections.ObjectModel;
using System.Globalization;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using Smart_tripp.WPF.Commands;

namespace Smart_tripp.WPF.ViewModels;

public sealed class ExpenseEditViewModel : ViewModelBase
{
    private readonly Trip trip;
    private readonly BudgetService budget;
    private readonly ExpenseEditingService editing;
    private readonly IToolHost host;
    private Expense? selectedExpense;
    private string category = string.Empty;
    private string description = string.Empty;
    private string amount = string.Empty;
    private DateTime? date;

    public ExpenseEditViewModel(Trip trip, BudgetService budget,
        ExpenseEditingService editing, IToolHost host)
    {
        this.trip = trip;
        this.budget = budget;
        this.editing = editing;
        this.host = host;
        SaveCommand = new AsyncCommand(
            () => host.RunAsync("Expense.Edit", SaveAsync), () => SelectedExpense is not null);
        DeleteCommand = new AsyncCommand(
            () => host.RunAsync("Expense.Delete", DeleteAsync), () => SelectedExpense is not null);
    }

    public ObservableCollection<Expense> Expenses { get; } = [];

    public AsyncCommand SaveCommand { get; }

    public AsyncCommand DeleteCommand { get; }

    public DateTime TripStart => trip.StartDate.Date;

    public DateTime TripEnd => trip.EndDate.Date;

    public Expense? SelectedExpense
    {
        get => selectedExpense;
        set
        {
            if (!SetProperty(ref selectedExpense, value))
            {
                return;
            }

            // Поля форми заповнюються з вибраної витрати.
            Category = value?.Category ?? string.Empty;
            Description = value?.Description ?? string.Empty;
            Amount = value?.Amount.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;
            Date = value?.Date.Date;
            SaveCommand.RaiseCanExecuteChanged();
            DeleteCommand.RaiseCanExecuteChanged();
        }
    }

    public string Category
    {
        get => category;
        set => SetProperty(ref category, value);
    }

    public string Description
    {
        get => description;
        set => SetProperty(ref description, value);
    }

    public string Amount
    {
        get => amount;
        set => SetProperty(ref amount, value);
    }

    public DateTime? Date
    {
        get => date;
        set => SetProperty(ref date, value);
    }

    public async Task<bool> LoadAsync()
    {
        var result = await budget.ListAsync(trip.TripId);
        if (result.IsFailure)
        {
            Status = result.Error;
            return false;
        }

        SelectedExpense = null;
        Expenses.Clear();
        foreach (Expense expense in result.Value!.OrderByDescending(x => x.Date))
        {
            Expenses.Add(expense);
        }

        return true;
    }

    private async Task SaveAsync()
    {
        Status = string.Empty;
        if (SelectedExpense is not { } expense)
        {
            return;
        }

        if (!decimal.TryParse(Amount.Trim().Replace(',', '.'),
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out decimal sum))
        {
            host.Reject("Expense.Edit", "Введіть суму числом, наприклад 1500,50.");
            return;
        }

        if (Date is not { } day)
        {
            host.Reject("Expense.Edit", "Виберіть дату.");
            return;
        }

        var result = await editing.UpdateAsync(expense.ExpenseId, Category, Description, sum, day);
        if (result.IsFailure)
        {
            Status = result.Error;
            return;
        }

        await host.RefreshAsync();
        Status = "Витрату оновлено.";
    }

    private async Task DeleteAsync()
    {
        Status = string.Empty;
        if (SelectedExpense is not { } expense
            || !host.Confirm($"Видалити витрату «{expense.Description}»?"))
        {
            return;
        }

        var result = await editing.DeleteAsync(expense.ExpenseId);
        if (result.IsFailure)
        {
            Status = result.Error;
            return;
        }

        await host.RefreshAsync();
        Status = "Витрату видалено.";
    }
}
