namespace SmartTrip.Domain.Entities;

public class Expense
{
    public int ExpenseId { get; set; }

    public int TripId { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime Date { get; set; }
}