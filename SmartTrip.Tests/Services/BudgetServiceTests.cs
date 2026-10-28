using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;
using Moq;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Tests.Services;

// Спільне налаштування: поточний користувач (Id = 1) і його подорож (Id = 1, 01.10–10.10.2024).
public abstract class BudgetServiceTestBase
{
    protected readonly Mock<IRepository<Expense>> mockExpenses;
    protected readonly Mock<IRepository<Place>> mockPlaces;
    protected readonly Mock<IRepository<Trip>> mockTrips;
    protected readonly Mock<IAppLogger> mockLogger;
    protected readonly UserSession userSession;
    protected readonly ServiceOperations operations;
    protected readonly TripService tripService;
    protected readonly BudgetService budgetService;

    protected BudgetServiceTestBase()
    {
        mockExpenses = new Mock<IRepository<Expense>>();
        mockPlaces = new Mock<IRepository<Place>>();
        mockTrips = new Mock<IRepository<Trip>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        tripService = new TripService(mockTrips.Object, userSession, operations);
        budgetService = new BudgetService(
            mockExpenses.Object,
            mockPlaces.Object,
            tripService,
            operations);

        var currentUser = new User { UserId = 1, FirstName = "Tourist", Role = "Tourist" };
        userSession.SignIn(currentUser);
    }

    protected Trip SetupOwnTrip(decimal budget = 5000, int people = 4)
    {
        var trip = new Trip
        {
            TripId = 1,
            UserId = 1,
            Title = "Paris Trip",
            City = "Paris",
            StartDate = new DateTime(2024, 10, 1),
            EndDate = new DateTime(2024, 10, 10),
            Budget = budget,
            PeopleCount = people
        };

        mockTrips.Setup(x => x.FindAsync(1)).ReturnsAsync(trip);
        return trip;
    }

    protected void SetupOtherUserTrip()
    {
        var trip = new Trip { TripId = 1, UserId = 999, Title = "Other Trip" };
        mockTrips.Setup(x => x.FindAsync(1)).ReturnsAsync(trip);
    }

    // Мок застосовує реальний предикат сервісу до бази витрат.
    protected void SetupExpenses(List<Expense> all)
    {
        mockExpenses
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<Expense, bool>>>()))
            .ReturnsAsync((Expression<Func<Expense, bool>> predicate) =>
                all.Where(predicate.Compile()).ToList());
    }

}

public class BudgetServiceAddExpenseTests : BudgetServiceTestBase
{
    [Fact]
    public async Task AddExpense_WithValidData_SavesExpense()
    {
        SetupOwnTrip();

        Expense? saved = null;
        mockExpenses
            .Setup(x => x.AddAsync(It.IsAny<Expense>()))
            .Callback<Expense>(e => saved = e)
            .Returns(Task.CompletedTask);

        await budgetService.AddExpenseAsync(
            1, "Food", "Dinner", 120, new DateTime(2024, 10, 3));

        Assert.NotNull(saved);
        Assert.Equal(1, saved!.TripId);
        Assert.Equal("Food", saved.Category);
        Assert.Equal("Dinner", saved.Description);
        Assert.Equal(120, saved.Amount);
        Assert.Equal(new DateTime(2024, 10, 3), saved.Date.Date);
        Assert.Equal(DateTimeKind.Utc, saved.Date.Kind);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Once);
    }

    [Fact]
    public async Task AddExpense_OnFirstDayOfTrip_Succeeds()
    {
        SetupOwnTrip();
        mockExpenses
            .Setup(x => x.AddAsync(It.IsAny<Expense>()))
            .Returns(Task.CompletedTask);

        await budgetService.AddExpenseAsync(
            1, "Food", "Breakfast", 20, new DateTime(2024, 10, 1));

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Once);
    }

    [Fact]
    public async Task AddExpense_OnLastDayOfTrip_Succeeds()
    {
        SetupOwnTrip();
        mockExpenses
            .Setup(x => x.AddAsync(It.IsAny<Expense>()))
            .Returns(Task.CompletedTask);

        await budgetService.AddExpenseAsync(
            1, "Food", "Farewell dinner", 80, new DateTime(2024, 10, 10));

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Once);
    }

    [Fact]
    public async Task AddExpense_WithEmptyCategory_ThrowsException()
    {
        SetupOwnTrip();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => budgetService.AddExpenseAsync(
                1, "", "Dinner", 120, new DateTime(2024, 10, 3)));

        Assert.Contains("Категорія", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }

    [Fact]
    public async Task AddExpense_WithEmptyDescription_ThrowsException()
    {
        SetupOwnTrip();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => budgetService.AddExpenseAsync(
                1, "Food", "", 120, new DateTime(2024, 10, 3)));

        Assert.Contains("Опис витрати", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task AddExpense_WithNonPositiveAmount_ThrowsException(int amount)
    {
        SetupOwnTrip();

        await Assert.ThrowsAsync<ArgumentException>(
            () => budgetService.AddExpenseAsync(
                1, "Food", "Dinner", amount, new DateTime(2024, 10, 3)));

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }

    [Fact]
    public async Task AddExpense_WithDateBeforeTripStart_ThrowsException()
    {
        SetupOwnTrip();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => budgetService.AddExpenseAsync(
                1, "Food", "Dinner", 120, new DateTime(2024, 9, 30)));

        Assert.Contains("в межах подорожі", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }

    [Fact]
    public async Task AddExpense_WithDateAfterTripEnd_ThrowsException()
    {
        SetupOwnTrip();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => budgetService.AddExpenseAsync(
                1, "Food", "Dinner", 120, new DateTime(2024, 10, 11)));

        Assert.Contains("в межах подорожі", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }

    [Fact]
    public async Task AddExpense_ToOtherUserTrip_ThrowsException()
    {
        SetupOtherUserTrip();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => budgetService.AddExpenseAsync(
                1, "Food", "Dinner", 120, new DateTime(2024, 10, 3)));

        Assert.Contains("власними подорожами", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }

    [Fact]
    public async Task AddExpense_ToNonexistentTrip_ThrowsException()
    {
        mockTrips.Setup(x => x.FindAsync(999)).ReturnsAsync((Trip?)null);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => budgetService.AddExpenseAsync(
                999, "Food", "Dinner", 120, new DateTime(2024, 10, 3)));

        Assert.Contains("Подорож не знайдена", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }
}

public class BudgetServiceAddPlaceExpenseTests : BudgetServiceTestBase
{
    [Fact]
    public async Task AddPlaceExpense_WithPaidPlace_SavesExpenseWithPlacePrice()
    {
        SetupOwnTrip();

        mockPlaces
            .Setup(x => x.FindAsync(5))
            .ReturnsAsync(new Place
            {
                PlaceId = 5,
                Name = "Louvre",
                Category = "Museum",
                Price = 150,
                Status = "Active"
            });

        Expense? saved = null;
        mockExpenses
            .Setup(x => x.AddAsync(It.IsAny<Expense>()))
            .Callback<Expense>(e => saved = e)
            .Returns(Task.CompletedTask);

        await budgetService.AddPlaceExpenseAsync(1, 5, new DateTime(2024, 10, 4));

        Assert.NotNull(saved);
        Assert.Equal(150, saved!.Amount);
        Assert.Equal("Museum", saved.Category);
        Assert.Equal("Відвідування: Louvre", saved.Description);
        Assert.Equal(1, saved.TripId);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Once);
    }

    [Fact]
    public async Task AddPlaceExpense_WithNonexistentPlace_ThrowsException()
    {
        SetupOwnTrip();
        mockPlaces.Setup(x => x.FindAsync(404)).ReturnsAsync((Place?)null);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => budgetService.AddPlaceExpenseAsync(1, 404, new DateTime(2024, 10, 4)));

        Assert.Contains("Місце не знайдено", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }

    [Fact]
    public async Task AddPlaceExpense_WithHiddenPlace_ThrowsException()
    {
        SetupOwnTrip();

        mockPlaces
            .Setup(x => x.FindAsync(5))
            .ReturnsAsync(new Place
            {
                PlaceId = 5,
                Name = "Closed Place",
                Category = "Museum",
                Price = 100,
                Status = "Hidden"
            });

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => budgetService.AddPlaceExpenseAsync(1, 5, new DateTime(2024, 10, 4)));

        Assert.Contains("недоступне", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }

    [Fact]
    public async Task AddPlaceExpense_WithFreePlace_ThrowsException()
    {
        SetupOwnTrip();

        mockPlaces
            .Setup(x => x.FindAsync(5))
            .ReturnsAsync(new Place
            {
                PlaceId = 5,
                Name = "City Park",
                Category = "Park",
                Price = 0,
                Status = "Active"
            });

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => budgetService.AddPlaceExpenseAsync(1, 5, new DateTime(2024, 10, 4)));

        Assert.Contains("безкоштовне", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }

    [Fact]
    public async Task AddPlaceExpense_WithDateOutsideTrip_ThrowsException()
    {
        SetupOwnTrip();

        mockPlaces
            .Setup(x => x.FindAsync(5))
            .ReturnsAsync(new Place
            {
                PlaceId = 5,
                Name = "Louvre",
                Category = "Museum",
                Price = 150,
                Status = "Active"
            });

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => budgetService.AddPlaceExpenseAsync(1, 5, new DateTime(2024, 11, 1)));

        Assert.Contains("в межах подорожі", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }

    [Fact]
    public async Task AddPlaceExpense_ToOtherUserTrip_ThrowsException()
    {
        SetupOtherUserTrip();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => budgetService.AddPlaceExpenseAsync(1, 5, new DateTime(2024, 10, 4)));

        Assert.Contains("власними подорожами", exception.Message);

        mockExpenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
    }
}

public class BudgetServiceCalculateTests : BudgetServiceTestBase
{
    [Fact]
    public async Task Calculate_WithExpenses_ReturnsCorrectSummary()
    {
        SetupOwnTrip(budget: 5000, people: 4);
        SetupExpenses(new List<Expense>
        {
            new Expense { TripId = 1, Category = "Food", Amount = 1000 },
            new Expense { TripId = 1, Category = "Museum", Amount = 500 }
        });

        var result = await budgetService.CalculateAsync(1);

        Assert.Equal(5000, result.Planned);
        Assert.Equal(1500, result.Spent);
        Assert.Equal(3500, result.Remaining);
        Assert.Equal(375, result.PerPerson);
    }

    [Fact]
    public async Task Calculate_WithoutExpenses_ReturnsFullBudgetRemaining()
    {
        SetupOwnTrip(budget: 5000, people: 4);
        SetupExpenses(new List<Expense>());

        var result = await budgetService.CalculateAsync(1);

        Assert.Equal(0, result.Spent);
        Assert.Equal(5000, result.Remaining);
        Assert.Equal(0, result.PerPerson);
    }

    [Fact]
    public async Task Calculate_WhenOverBudget_ReturnsNegativeRemaining()
    {
        SetupOwnTrip(budget: 5000, people: 4);
        SetupExpenses(new List<Expense>
        {
            new Expense { TripId = 1, Category = "Hotel", Amount = 6000 }
        });

        var result = await budgetService.CalculateAsync(1);

        Assert.Equal(-1000, result.Remaining);
    }

    [Fact]
    public async Task Calculate_RoundsPerPersonToTwoDecimals()
    {
        SetupOwnTrip(budget: 5000, people: 3);
        SetupExpenses(new List<Expense>
        {
            new Expense { TripId = 1, Category = "Food", Amount = 100 }
        });

        var result = await budgetService.CalculateAsync(1);

        Assert.Equal(33.33m, result.PerPerson);
    }

    [Fact]
    public async Task Calculate_WithZeroPeople_ReturnsZeroPerPerson()
    {
        SetupOwnTrip(budget: 5000, people: 0);
        SetupExpenses(new List<Expense>
        {
            new Expense { TripId = 1, Category = "Food", Amount = 100 }
        });

        var result = await budgetService.CalculateAsync(1);

        Assert.Equal(0, result.PerPerson);
    }

    [Fact]
    public async Task Calculate_CountsOnlyExpensesOfThisTrip()
    {
        SetupOwnTrip(budget: 5000, people: 4);
        SetupExpenses(new List<Expense>
        {
            new Expense { TripId = 1, Category = "Food", Amount = 200 },
            new Expense { TripId = 2, Category = "Food", Amount = 9999 }
        });

        var result = await budgetService.CalculateAsync(1);

        Assert.Equal(200, result.Spent);
    }

    [Fact]
    public async Task Calculate_ForOtherUserTrip_ThrowsException()
    {
        SetupOtherUserTrip();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => budgetService.CalculateAsync(1));

        Assert.Contains("власними подорожами", exception.Message);
    }
}

public class BudgetServiceListExpensesTests : BudgetServiceTestBase
{
    [Fact]
    public async Task ListExpenses_ReturnsOnlyExpensesOfThisTrip()
    {
        SetupOwnTrip();
        SetupExpenses(new List<Expense>
        {
            new Expense { TripId = 1, Description = "Dinner", Amount = 100 },
            new Expense { TripId = 2, Description = "Other trip expense", Amount = 50 },
            new Expense { TripId = 1, Description = "Museum", Amount = 30 }
        });

        var result = await budgetService.ListAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal(1, e.TripId));
        Assert.DoesNotContain(result, e => e.Description == "Other trip expense");
    }

    [Fact]
    public async Task ListExpenses_ReturnsEmptyWhenTripHasNoExpenses()
    {
        SetupOwnTrip();
        SetupExpenses(new List<Expense>());

        var result = await budgetService.ListAsync(1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListExpenses_ForOtherUserTrip_ThrowsException()
    {
        SetupOtherUserTrip();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => budgetService.ListAsync(1));

        Assert.Contains("власними подорожами", exception.Message);

        mockExpenses.Verify(
            x => x.ListAsync(It.IsAny<Expression<Func<Expense, bool>>>()),
            Times.Never);
    }
}

public class BudgetServiceStatisticsTests : BudgetServiceTestBase
{
    [Fact]
    public async Task Statistics_GroupsExpensesByCategory()
    {
        SetupOwnTrip();
        SetupExpenses(new List<Expense>
        {
            new Expense { TripId = 1, Category = "Food", Amount = 100 },
            new Expense { TripId = 1, Category = "Food", Amount = 60 },
            new Expense { TripId = 1, Category = "Museum", Amount = 300 }
        });

        var result = await budgetService.StatisticsAsync(1);

        Assert.Equal(2, result.Count);
        Assert.Equal(160, result.Single(x => x.Category == "Food").Total);
        Assert.Equal(300, result.Single(x => x.Category == "Museum").Total);
    }

    [Fact]
    public async Task Statistics_MergesCategoriesIgnoringCase()
    {
        SetupOwnTrip();
        SetupExpenses(new List<Expense>
        {
            new Expense { TripId = 1, Category = "Food", Amount = 100 },
            new Expense { TripId = 1, Category = "food", Amount = 50 }
        });

        var result = await budgetService.StatisticsAsync(1);

        Assert.Single(result);
        Assert.Equal(150, result[0].Total);
    }

    [Fact]
    public async Task Statistics_SortsCategoriesByTotalDescending()
    {
        SetupOwnTrip();
        SetupExpenses(new List<Expense>
        {
            new Expense { TripId = 1, Category = "Food", Amount = 100 },
            new Expense { TripId = 1, Category = "Museum", Amount = 300 },
            new Expense { TripId = 1, Category = "Transport", Amount = 200 }
        });

        var result = await budgetService.StatisticsAsync(1);

        Assert.Equal(
            new[] { "Museum", "Transport", "Food" },
            result.Select(x => x.Category).ToArray());
    }

    [Fact]
    public async Task Statistics_CountsOnlyExpensesOfThisTrip()
    {
        SetupOwnTrip();
        SetupExpenses(new List<Expense>
        {
            new Expense { TripId = 1, Category = "Food", Amount = 100 },
            new Expense { TripId = 2, Category = "Food", Amount = 9999 }
        });

        var result = await budgetService.StatisticsAsync(1);

        Assert.Single(result);
        Assert.Equal(100, result[0].Total);
    }

    [Fact]
    public async Task Statistics_WithoutExpenses_ReturnsEmptyList()
    {
        SetupOwnTrip();
        SetupExpenses(new List<Expense>());

        var result = await budgetService.StatisticsAsync(1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Statistics_ForOtherUserTrip_ThrowsException()
    {
        SetupOtherUserTrip();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => budgetService.StatisticsAsync(1));

        Assert.Contains("власними подорожами", exception.Message);
    }
}