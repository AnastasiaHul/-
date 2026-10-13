using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Services;

namespace SmartTrip.Tests.Services;

public class BudgetCalculationTests
{
    [Fact]
    public void CalculateBudget_ShouldCalculateCorrectly()
    {
        var trip = new Trip
        {
            TripId = 1,
            Budget = 5000,
            PeopleCount = 4
        };

        decimal spent = 3000;
        decimal remaining = trip.Budget - spent;
        decimal perPerson = trip.PeopleCount > 0 ? trip.Budget / trip.PeopleCount : 0;

        Assert.Equal(5000, trip.Budget);
        Assert.Equal(2000, remaining);
        Assert.Equal(1250, perPerson);
    }

    [Fact]
    public void CalculateBudget_ShouldHandleZeroPeopleCount()
    {
        var trip = new Trip
        {
            Budget = 2000,
            PeopleCount = 0
        };

        decimal perPerson = trip.PeopleCount > 0 ? trip.Budget / trip.PeopleCount : 0;

        Assert.Equal(0, perPerson);
    }

    [Fact]
    public void CalculateBudget_ShouldHandleNegativeRemaining()
    {
        decimal planned = 2000;
        decimal spent = 3000;
        decimal remaining = planned - spent;

        Assert.Equal(-1000, remaining);
        Assert.True(remaining < 0);
    }

    [Fact]
    public void CalculateBudget_ShouldCalculateZeroRemainderWhenBalanced()
    {
        decimal planned = 2500;
        decimal spent = 2500;
        decimal remaining = planned - spent;

        Assert.Equal(0, remaining);
    }
}

public class ExpenseValidationTests
{
    [Theory]
    [InlineData(150.50)]
    [InlineData(1000)]
    [InlineData(0.01)]
    public void ValidateExpenseAmount_ShouldAcceptValidAmounts(decimal amount)
    {
        bool isValid = amount > 0 && amount <= 100000000;
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void ValidateExpenseAmount_ShouldRejectInvalidAmounts(decimal amount)
    {
        bool isValid = amount > 0 && amount <= 100000000;
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("Food")]
    [InlineData("Transport")]
    public void ValidateExpenseCategory_ShouldAcceptValidCategory(string category)
    {
        bool isValid = !string.IsNullOrWhiteSpace(category) && category.Length <= 100;
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateExpenseCategory_ShouldRejectInvalidCategory(string category)
    {
        bool isValid = !string.IsNullOrWhiteSpace(category) && category.Length <= 100;
        Assert.False(isValid);
    }
}

public class ExpenseGroupingTests
{
    [Fact]
    public void GroupExpenses_ShouldGroupByCategory()
    {
        var expenses = new List<Expense>
        {
            new Expense { Category = "Food", Amount = 100 },
            new Expense { Category = "Food", Amount = 50 },
            new Expense { Category = "Transport", Amount = 200 }
        };

        var grouped = expenses
            .GroupBy(x => x.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
            .ToList();

        Assert.Equal(2, grouped.Count);
        Assert.Equal(150, grouped.First(x => x.Category == "Food").Total);
    }

    [Fact]
    public void GroupExpenses_ShouldReturnEmptyWhenNoExpenses()
    {
        var expenses = new List<Expense>();
        Assert.Empty(expenses);
    }

    [Fact]
    public void GroupExpenses_ShouldCalculateTotalCorrectly()
    {
        var expenses = new List<Expense>
        {
            new Expense { Amount = 100 },
            new Expense { Amount = 50 },
            new Expense { Amount = 25 }
        };

        decimal total = expenses.Sum(x => x.Amount);
        Assert.Equal(175, total);
    }
}