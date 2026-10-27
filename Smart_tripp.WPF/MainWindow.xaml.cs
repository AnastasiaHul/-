using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class MainWindow : Window
{
    private readonly AuthService auth;
    private readonly TripService trips;
    private readonly PlaceService places;
    private readonly CatalogService catalog;
    private readonly BudgetService budget;
    private readonly PackingService packing;
    private readonly UserSession session;
    private readonly IAppLogger logger;
    private readonly ItineraryService itinerary;
    private readonly MapRouteService mapRoutes;

    private bool busy;
    private int? editingTripId;
    private int? editingPlaceId;

    public MainWindow(
        AuthService auth,
        TripService trips,
        PlaceService places,
        CatalogService catalog,
        BudgetService budget,
        PackingService packing,
        UserSession session,
        IAppLogger logger,
        ItineraryService itinerary,
        MapRouteService mapRoutes)

    {
        this.auth = auth;
        this.trips = trips;
        this.places = places;
        this.catalog = catalog;
        this.budget = budget;
        this.packing = packing;
        this.session = session;
        this.logger = logger;
        this.itinerary = itinerary;
        this.mapRoutes = mapRoutes;

        InitializeComponent();

        ClearTripEditor();
        ClearPlaceEditor();
        UpdateAccountNavigation();

        Loaded += async (_, _) => await ExecuteAsync(SearchAsync);
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (busy)
        {
            return;
        }

        busy = true;

        if (activeDialog is not null)
        {
            activeDialog.IsEnabled = false;
        }
        else
        {
            IsEnabled = false;
        }

        StatusText.Text = "Виконується…";

        try
        {
            await action();
            StatusText.Text = "Готово. Дані оновлено.";
        }
        catch (Exception exception)
        {
            logger.Error(
                exception,
                "WpfAction",
                session.CurrentUser?.UserId);

            string message =
                exception is ArgumentException
                    or UnauthorizedAccessException
                    ? exception.Message
                    : exception is InvalidOperationException
                        && session.CurrentUser is null
                        ? "Спочатку увійдіть у систему."
                        : "Дію не виконано. Перевірте підключення "
                            + "до бази; деталі збережено в журналі.";

            StatusText.Text = message;

            MessageBox.Show(
                activeDialog ?? this,
                message,
                "Smart Trip",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;

            if (activeDialog is not null)
            {
                activeDialog.IsEnabled = true;
            }

            busy = false;
        }
    }

    private async void OnAuthAction(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            string action = UiInput.Action(sender);

            if (action == "Register")
            {
                string role =
                    ((ComboBoxItem)RoleBox.SelectedItem)
                    .Tag.ToString() ?? "Tourist";

                await auth.RegisterAsync(
                    EmailBox.Text,
                    PasswordBox.Password,
                    NameBox.Text,
                    role);
            }
            else if (action == "Login")
            {
                await auth.LoginAsync(
                    EmailBox.Text,
                    PasswordBox.Password);
            }
            else
            {
                await auth.LogoutAsync();
            }

            PasswordBox.Clear();
            ClearTripEditor();
            ClearPlaceEditor();
            OnlyFavorites.IsChecked = false;

            User? user = session.CurrentUser;

            SessionText.Text = user is null
                ? "Гість — переглядайте каталог або увійдіть"
                : $"{user.FirstName} · "
                    + (user.Role == "Partner"
                        ? "Партнер"
                        : "Турист");

            LoginPanel.Visibility = user is null
                ? Visibility.Visible
                : Visibility.Collapsed;

            LogoutButton.Visibility = user is null
                ? Visibility.Collapsed
                : Visibility.Visible;

            TripsTab.IsEnabled = user?.Role == "Tourist";
            BudgetTab.IsEnabled = user?.Role == "Tourist";
            PlacesTab.IsEnabled = user?.Role == "Partner";

            Tabs.SelectedIndex = user?.Role == "Partner"
                ? 1
                : user is null ? 2 : 0;

            await RefreshAllAsync();

            UpdateAccountNavigation();
            CompleteDialog();
        });
    }

    private async Task RefreshAllAsync()
    {
        TripsGrid.ItemsSource = null;
        BudgetTrip.ItemsSource = null;
        PlacesGrid.ItemsSource = null;
        ExpensesGrid.ItemsSource = null;
        PackingGrid.ItemsSource = null;
        StatisticsGrid.ItemsSource = null;

        BudgetSummaryText.Text = "Виберіть подорож";

        if (session.CurrentUser?.Role == "Tourist")
        {
            await RefreshTripsAsync();
        }

        if (session.CurrentUser?.Role == "Partner")
        {
            await RefreshPlacesAsync();
        }

        await SearchAsync();
    }

    private async Task RefreshTripsAsync()
    {
        int? selectedId =
            (BudgetTrip.SelectedItem as Trip)?.TripId;

        IReadOnlyList<Trip> result = await trips.ListAsync();

        TripsGrid.ItemsSource = result
            .OrderByDescending(x => x.StartDate)
            .ToList();

        BudgetTrip.ItemsSource = result;

        BudgetTrip.SelectedItem =
            result.FirstOrDefault(x => x.TripId == selectedId)
            ?? result.FirstOrDefault();

        if (BudgetTrip.SelectedItem is Trip)
        {
            await RefreshBudgetAsync();
        }
        else
        {
            ExpensesGrid.ItemsSource = null;
            PackingGrid.ItemsSource = null;
            StatisticsGrid.ItemsSource = null;

            BudgetSummaryText.Text = "Виберіть подорож";
        }
    }

    private async void OnTripAction(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            switch (UiInput.Action(sender))
            {
                case "New":
                    TripsGrid.SelectedItem = null;
                    ClearTripEditor();
                    return;

                case "Save":
                    await trips.SaveAsync(
                        editingTripId,
                        TripTitle.Text,
                        TripCity.Text,
                        UiInput.Date(TripStart),
                        UiInput.Date(TripEnd),
                        UiInput.Money(TripBudget),
                        UiInput.Integer(TripPeople),
                        TripInterests.Text);

                    ClearTripEditor();
                    break;

                case "Delete":
                    Trip selected =
                        UiInput.Selected<Trip>(TripsGrid);

                    if (!Confirm(
                        $"Видалити подорож «{selected.Title}» "
                        + "разом із витратами та списком речей?"))
                    {
                        return;
                    }

                    await trips.DeleteAsync(selected.TripId);

                    ClearTripEditor();
                    break;
            }

            await RefreshTripsAsync();

            if (UiInput.Action(sender) == "Save")
            {
                CompleteDialog();
            }
        });
    }

    private void OnTripSelected(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (TripsGrid.SelectedItem is not Trip trip)
        {
            return;
        }

        editingTripId = trip.TripId;
        TripTitle.Text = trip.Title;
        TripCity.Text = trip.City;
        TripStart.SelectedDate = trip.StartDate;
        TripEnd.SelectedDate = trip.EndDate;

        TripBudget.Text = trip.Budget.ToString(
            CultureInfo.CurrentCulture);

        TripPeople.Text = trip.PeopleCount.ToString(
            CultureInfo.CurrentCulture);

        TripInterests.Text = trip.Interests;
    }

    private void ClearTripEditor()
    {
        editingTripId = null;

        TripTitle.Clear();
        TripCity.Clear();
        TripStart.SelectedDate = DateTime.Today;
        TripEnd.SelectedDate = DateTime.Today.AddDays(1);
        TripBudget.Text = "0";
        TripPeople.Text = "1";
        TripInterests.Clear();
    }

    private async Task RefreshPlacesAsync()
    {
        PlacesGrid.ItemsSource = await places.ListOwnAsync();
    }

    private async void OnPlaceAction(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            switch (UiInput.Action(sender))
            {
                case "New":
                    PlacesGrid.SelectedItem = null;
                    ClearPlaceEditor();
                    return;

                case "Save":
                    await places.SaveAsync(
                        editingPlaceId,
                        PlaceName.Text,
                        PlaceCategory.Text,
                        UiInput.Money(PlacePrice),
                        PlaceAddress.Text,
                        PlaceSchedule.Text,
                        PlaceDescription.Text);

                    ClearPlaceEditor();
                    break;

                case "Delete":
                    Place selected =
                        UiInput.Selected<Place>(PlacesGrid);

                    if (!Confirm(
                        $"Видалити місце «{selected.Name}» "
                        + "разом із відгуками та записами обраного?"))
                    {
                        return;
                    }

                    await places.DeleteAsync(selected.PlaceId);

                    ClearPlaceEditor();
                    break;
            }

            await RefreshPlacesAsync();
            await SearchAsync();

            if (UiInput.Action(sender) == "Save")
            {
                CompleteDialog();
            }
        });
    }

    private void OnPlaceSelected(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (PlacesGrid.SelectedItem is not Place place)
        {
            return;
        }

        editingPlaceId = place.PlaceId;
        PlaceName.Text = place.Name;
        PlaceCategory.Text = place.Category;

        PlacePrice.Text = place.Price.ToString(
            CultureInfo.CurrentCulture);

        PlaceAddress.Text = place.Address;
        PlaceSchedule.Text = place.Schedule;
        PlaceDescription.Text = place.Description;
    }

    private void ClearPlaceEditor()
    {
        editingPlaceId = null;

        PlaceName.Clear();
        PlaceCategory.Clear();
        PlacePrice.Text = "0";
        PlaceAddress.Clear();
        PlaceSchedule.Clear();
        PlaceDescription.Clear();
    }

    private async Task SearchAsync()
    {
        decimal? maxPrice =
            string.IsNullOrWhiteSpace(SearchPrice.Text)
                ? null
                : UiInput.Money(SearchPrice);

        CatalogGrid.ItemsSource = await catalog.SearchAsync(
            SearchText.Text,
            SearchCategory.Text,
            maxPrice,
            OnlyFavorites.IsChecked == true);
    }

    private async void OnCatalogAction(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            string action = UiInput.Action(sender);

            if (action == "Search")
            {
                await SearchAsync();
                return;
            }

            Place selected =
                UiInput.Selected<Place>(CatalogGrid);

            if (action == "Details")
            {
                PlaceDetails details =
                    await catalog.DetailsAsync(selected.PlaceId);

                string average =
                    details.AverageRating?.ToString(
                        CultureInfo.CurrentCulture)
                    ?? "немає оцінок";

                string text =
                    $"{details.Place.Name}\n"
                    + $"Категорія: {details.Place.Category}\n"
                    + $"Ціна: {details.Place.Price:N2} ₴\n"
                    + $"Адреса: {details.Place.Address}\n"
                    + $"Графік: {details.Place.Schedule}\n\n"
                    + $"{details.Place.Description}\n\n"
                    + $"Середня оцінка: {average}\n\n";

                text += string.Join(
                    "\n\n",
                    details.Reviews
                        .OrderByDescending(x => x.CreatedAt)
                        .Select(x =>
                            $"{x.CreatedAt:dd.MM.yyyy} · "
                            + $"{x.Rating}/5\n{x.Comment}"));

                ShowDetails(text);
            }
            else if (action == "Favorite")
            {
                await catalog.ToggleFavoriteAsync(
                    selected.PlaceId);

                await SearchAsync();
            }
            else if (action == "Review")
            {
                await catalog.AddReviewAsync(
                    selected.PlaceId,
                    RatingBox.SelectedIndex + 1,
                    ReviewComment.Text);

                ReviewComment.Clear();
                CompleteDialog();
            }
        });
    }

    private void ShowDetails(string text)
    {
        var details = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0)
        };

        CreateDialog(
            "Деталі місця та відгуки",
            details,
            720).ShowDialog();
    }

    private int SelectedTripId()
    {
        return (BudgetTrip.SelectedItem as Trip)?.TripId
            ?? throw new ArgumentException(
                "Спочатку створіть або виберіть подорож.");
    }

    private async void OnBudgetTripChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!busy && BudgetTrip.SelectedItem is Trip)
        {
            await ExecuteAsync(RefreshBudgetAsync);
        }
    }

    private async Task RefreshBudgetAsync()
    {
        int tripId = SelectedTripId();

        BudgetSummary summary =
            await budget.CalculateAsync(tripId);

        BudgetSummaryText.Text =
            $"Заплановано: {summary.Planned:N2} ₴ · "
            + $"Витрачено: {summary.Spent:N2} ₴ · "
            + $"Залишок: {summary.Remaining:N2} ₴ · "
            + $"На людину: {summary.PerPerson:N2} ₴";

        ExpensesGrid.ItemsSource =
            await budget.ListAsync(tripId);

        StatisticsGrid.ItemsSource =
            await budget.StatisticsAsync(tripId);

        PackingGrid.ItemsSource =
            await packing.ListAsync(tripId);

        Trip trip = (Trip)BudgetTrip.SelectedItem;

        if (ExpenseDate.SelectedDate is null
            || ExpenseDate.SelectedDate < trip.StartDate.Date
            || ExpenseDate.SelectedDate > trip.EndDate.Date)
        {
            ExpenseDate.SelectedDate = trip.StartDate.Date;
        }
    }

    private async void OnBudgetAction(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            int tripId = SelectedTripId();

            if (UiInput.Action(sender) == "Expense")
            {
                await budget.AddExpenseAsync(
                    tripId,
                    ExpenseCategory.Text,
                    ExpenseDescription.Text,
                    UiInput.Money(ExpenseAmount),
                    UiInput.Date(ExpenseDate));

                ExpenseAmount.Clear();
                ExpenseDescription.Clear();
            }

            await RefreshBudgetAsync();

            if (UiInput.Action(sender) == "Expense")
            {
                CompleteDialog();
            }
        });
    }

    private async void OnPackingAction(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            switch (UiInput.Action(sender))
            {
                case "Add":
                    await packing.AddAsync(
                        SelectedTripId(),
                        PackingName.Text);

                    PackingName.Clear();
                    break;

                case "Toggle":
                    await packing.ToggleAsync(
                        UiInput.Selected<PackingItem>(
                            PackingGrid).PackingItemId);
                    break;

                case "Delete":
                    await packing.DeleteAsync(
                        UiInput.Selected<PackingItem>(
                            PackingGrid).PackingItemId);
                    break;
            }

            await RefreshBudgetAsync();
        });
    }

    private bool Confirm(string message)
    {
        return MessageBox.Show(
            this,
            message,
            "Підтвердження",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question)
            == MessageBoxResult.Yes;
    }
}