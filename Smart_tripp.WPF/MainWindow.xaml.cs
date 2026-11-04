using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Application.Common;
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
    private bool validationFailed;
    private int? editingTripId;
    private int? editingPlaceId;
    private async Task ExecuteAsync(Func<Task> action)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        validationFailed = false;
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
            if (!validationFailed)
            {
                StatusText.Text = "Готово. Дані оновлено.";
            }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "WpfAction", session.CurrentUser?.UserId);
            string message = exception is ArgumentException or UnauthorizedAccessException ? exception.Message : exception is InvalidOperationException && session.CurrentUser is null ? "Спочатку увійдіть у систему." : "Дію не виконано. Перевірте підключення " + "до бази; деталі збережено в журналі.";
            StatusText.Text = message;
            MessageBox.Show(activeDialog ?? this, message, "Smart Trip", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    private void ShowValidationError(string message, bool log = false)
    {
        validationFailed = true;
        StatusText.Text = message;
        if (log)
        {
            logger.Information($"WpfValidationFailed: {message}", session.CurrentUser?.UserId);
        }

        MessageBox.Show(activeDialog ?? this, message, "Перевірте введені дані", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private Result<decimal> ReadPlacePrice()
    {
        string text = PlacePrice.Text.Trim().Replace(',', '.');
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal price))
        {
            return Result<decimal>.Failure("Ціна: введіть коректне число, наприклад 350 або 350,50.");
        }

        return Result<decimal>.Success(price);
    }

    private async void OnAuthAction(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            string action = UiInput.Action(sender);
            switch (action)
            {
                case "Register":
                {
                    string role = (RoleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
                    Result<User> registered = await auth.RegisterAsync(EmailBox.Text, PasswordBox.Password, NameBox.Text, role);
                    if (registered.IsFailure)
                    {
                        ShowValidationError(registered.Error);
                        return;
                    }

                    break;
                }

                case "Login":
                {
                    Result<User> loggedIn = await auth.LoginAsync(EmailBox.Text, PasswordBox.Password);
                    if (loggedIn.IsFailure)
                    {
                        ShowValidationError(loggedIn.Error);
                        return;
                    }

                    break;
                }

                case "Logout":
                {
                    Result loggedOut = await auth.LogoutAsync();
                    if (loggedOut.IsFailure)
                    {
                        ShowValidationError(loggedOut.Error);
                        return;
                    }

                    break;
                }

                default:
                    ShowValidationError("Невідома дія авторизації.", log: true);
                    return;
            }

            PasswordBox.Clear();
            ClearTripEditor();
            ClearPlaceEditor();
            OnlyFavorites.IsChecked = false;
            User? user = session.CurrentUser;
            SessionText.Text = user is null ? "Гість — переглядайте каталог або увійдіть" : $"{user.FirstName} · " + (user.Role == "Partner" ? "Партнер" : "Турист");
            LoginPanel.Visibility = user is null ? Visibility.Visible : Visibility.Collapsed;
            LogoutButton.Visibility = user is null ? Visibility.Collapsed : Visibility.Visible;
            TripsTab.IsEnabled = user?.Role == "Tourist";
            BudgetTab.IsEnabled = user?.Role == "Tourist";
            PlacesTab.IsEnabled = user?.Role == "Partner";
            Tabs.SelectedIndex = user?.Role == "Partner" ? 1 : user is null ? 2 : 0;
            await RefreshAllAsync();
            UpdateAccountNavigation();
            if (!validationFailed)
            {
                CompleteDialog();
            }
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
        int? selectedId = (BudgetTrip.SelectedItem as Trip)?.TripId;
        var check1 = await trips.ListAsync();
        if (check1.IsFailure)
        {
            ShowValidationError(check1.Error, log: false);
            return;
        }

        IReadOnlyList<Trip> result = check1.Value!;
        TripsGrid.ItemsSource = result.OrderByDescending(x => x.StartDate).ToList();
        BudgetTrip.ItemsSource = result;
        BudgetTrip.SelectedItem = result.FirstOrDefault(x => x.TripId == selectedId) ?? result.FirstOrDefault();
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

    private async void OnTripAction(object sender, RoutedEventArgs e)
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
                    var check2 = UiInput.Date(TripStart);
                    if (check2.IsFailure)
                    {
                        ShowValidationError(check2.Error, log: true);
                        return;
                    }

                    var check3 = UiInput.Date(TripEnd);
                    if (check3.IsFailure)
                    {
                        ShowValidationError(check3.Error, log: true);
                        return;
                    }

                    var check4 = UiInput.Money(TripBudget);
                    if (check4.IsFailure)
                    {
                        ShowValidationError(check4.Error, log: true);
                        return;
                    }

                    var check5 = UiInput.Integer(TripPeople);
                    if (check5.IsFailure)
                    {
                        ShowValidationError(check5.Error, log: true);
                        return;
                    }

                    var check6 = await trips.SaveAsync(editingTripId, TripTitle.Text, TripCity.Text, check2.Value!, check3.Value!, check4.Value!, check5.Value!, TripInterests.Text);
                    if (check6.IsFailure)
                    {
                        ShowValidationError(check6.Error, log: false);
                        return;
                    }

                    ClearTripEditor();
                    break;
                case "Delete":
                    var check7 = UiInput.Selected<Trip>(TripsGrid);
                    if (check7.IsFailure)
                    {
                        ShowValidationError(check7.Error, log: true);
                        return;
                    }

                    Trip selected = check7.Value!;
                    if (!Confirm($"Видалити подорож «{selected.Title}» " + "разом із витратами та списком речей?"))
                    {
                        return;
                    }

                    var check8 = await trips.DeleteAsync(selected.TripId);
                    if (check8.IsFailure)
                    {
                        ShowValidationError(check8.Error, log: false);
                        return;
                    }

                    ClearTripEditor();
                    break;
            }

            await RefreshTripsAsync();
            if (!validationFailed && UiInput.Action(sender) == "Save")
            {
                CompleteDialog();
            }
        });
    }

    private void OnTripSelected(object sender, SelectionChangedEventArgs e)
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
        TripBudget.Text = trip.Budget.ToString(CultureInfo.CurrentCulture);
        TripPeople.Text = trip.PeopleCount.ToString(CultureInfo.CurrentCulture);
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
        Result<IReadOnlyList<Place>> result = await places.ListOwnAsync();
        if (result.IsFailure)
        {
            PlacesGrid.ItemsSource = null;
            ShowValidationError(result.Error);
            return;
        }

        PlacesGrid.ItemsSource = result.Value;
    }

    private async void OnPlaceAction(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            string action = UiInput.Action(sender);
            switch (action)
            {
                case "New":
                    PlacesGrid.SelectedItem = null;
                    ClearPlaceEditor();
                    return;
                case "Save":
                {
                    Result<decimal> price = ReadPlacePrice();
                    if (price.IsFailure)
                    {
                        ShowValidationError(price.Error, log: true);
                        return;
                    }

                    Result<Place> saved = await places.SaveAsync(editingPlaceId, PlaceName.Text, PlaceCategory.Text, price.Value, PlaceAddress.Text, PlaceSchedule.Text, PlaceDescription.Text);
                    if (saved.IsFailure)
                    {
                        ShowValidationError(saved.Error);
                        return;
                    }

                    ClearPlaceEditor();
                    break;
                }

                case "Delete":
                {
                    if (PlacesGrid.SelectedItem is not Place selected)
                    {
                        ShowValidationError("Спочатку виберіть місце в таблиці.", log: true);
                        return;
                    }

                    if (!Confirm($"Видалити місце «{selected.Name}» " + "разом із відгуками та записами обраного?"))
                    {
                        return;
                    }

                    Result deleted = await places.DeleteAsync(selected.PlaceId);
                    if (deleted.IsFailure)
                    {
                        ShowValidationError(deleted.Error);
                        return;
                    }

                    ClearPlaceEditor();
                    break;
                }
            }

            await RefreshPlacesAsync();
            if (validationFailed)
            {
                return;
            }

            await SearchAsync();
            if (action == "Save")
            {
                CompleteDialog();
            }
        });
    }

    private void OnPlaceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (PlacesGrid.SelectedItem is not Place place)
        {
            return;
        }

        editingPlaceId = place.PlaceId;
        PlaceName.Text = place.Name;
        PlaceCategory.Text = place.Category;
        PlacePrice.Text = place.Price.ToString(CultureInfo.CurrentCulture);
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
        decimal? maxPrice;
        if (string.IsNullOrWhiteSpace(SearchPrice.Text))
        {
            maxPrice = null;
        }
        else
        {
            var check9 = UiInput.Money(SearchPrice);
            if (check9.IsFailure)
            {
                ShowValidationError(check9.Error, log: true);
                return;
            }

            maxPrice = check9.Value!;
        }

        var check10 = await catalog.SearchAsync(SearchText.Text, SearchCategory.Text, maxPrice, OnlyFavorites.IsChecked == true);
        if (check10.IsFailure)
        {
            ShowValidationError(check10.Error, log: false);
            return;
        }

        CatalogGrid.ItemsSource = check10.Value!;
    }

    private async void OnCatalogAction(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            string action = UiInput.Action(sender);
            if (action == "Search")
            {
                await SearchAsync();
                return;
            }

            var check11 = UiInput.Selected<Place>(CatalogGrid);
            if (check11.IsFailure)
            {
                ShowValidationError(check11.Error, log: true);
                return;
            }

            Place selected = check11.Value!;
            if (action == "Details")
            {
                var check12 = await catalog.DetailsAsync(selected.PlaceId);
                if (check12.IsFailure)
                {
                    ShowValidationError(check12.Error, log: false);
                    return;
                }

                PlaceDetails details = check12.Value!;
                string average = details.AverageRating?.ToString(CultureInfo.CurrentCulture) ?? "немає оцінок";
                string text = $"{details.Place.Name}\n" + $"Категорія: {details.Place.Category}\n" + $"Ціна: {details.Place.Price:N2} ₴\n" + $"Адреса: {details.Place.Address}\n" + $"Графік: {details.Place.Schedule}\n\n" + $"{details.Place.Description}\n\n" + $"Середня оцінка: {average}\n\n";
                text += string.Join("\n\n", details.Reviews.OrderByDescending(x => x.CreatedAt).Select(x => $"{x.CreatedAt:dd.MM.yyyy} · " + $"{x.Rating}/5\n{x.Comment}"));
                ShowDetails(text);
            }
            else if (action == "Favorite")
            {
                var check13 = await catalog.ToggleFavoriteAsync(selected.PlaceId);
                if (check13.IsFailure)
                {
                    ShowValidationError(check13.Error, log: false);
                    return;
                }

                await SearchAsync();
            }
            else if (action == "Review")
            {
                var check14 = await catalog.AddReviewAsync(selected.PlaceId, RatingBox.SelectedIndex + 1, ReviewComment.Text);
                if (check14.IsFailure)
                {
                    ShowValidationError(check14.Error, log: false);
                    return;
                }

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
        CreateDialog("Деталі місця та відгуки", details, 720).ShowDialog();
    }

    private Result<int> SelectedTripId()
    {
        var value15 = (BudgetTrip.SelectedItem as Trip)?.TripId;
        if (value15 is null)
        {
            return new Failure("Спочатку створіть або виберіть подорож.");
        }

        return value15.Value;
    }

    private async void OnBudgetTripChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!busy && BudgetTrip.SelectedItem is Trip)
        {
            await ExecuteAsync(RefreshBudgetAsync);
        }
    }

    private async Task RefreshBudgetAsync()
    {
        var check16 = SelectedTripId();
        if (check16.IsFailure)
        {
            ShowValidationError(check16.Error, log: true);
            return;
        }

        int tripId = check16.Value!;
        var check17 = await budget.CalculateAsync(tripId);
        if (check17.IsFailure)
        {
            ShowValidationError(check17.Error, log: false);
            return;
        }

        BudgetSummary summary = check17.Value!;
        BudgetSummaryText.Text = $"Заплановано: {summary.Planned:N2} ₴ · " + $"Витрачено: {summary.Spent:N2} ₴ · " + $"Залишок: {summary.Remaining:N2} ₴ · " + $"На людину: {summary.PerPerson:N2} ₴";
        var check18 = await budget.ListAsync(tripId);
        if (check18.IsFailure)
        {
            ShowValidationError(check18.Error, log: false);
            return;
        }

        ExpensesGrid.ItemsSource = check18.Value!;
        var check19 = await budget.StatisticsAsync(tripId);
        if (check19.IsFailure)
        {
            ShowValidationError(check19.Error, log: false);
            return;
        }

        StatisticsGrid.ItemsSource = check19.Value!;
        var check20 = await packing.ListAsync(tripId);
        if (check20.IsFailure)
        {
            ShowValidationError(check20.Error, log: false);
            return;
        }

        PackingGrid.ItemsSource = check20.Value!;
        Trip trip = (Trip)BudgetTrip.SelectedItem;
        if (ExpenseDate.SelectedDate is null || ExpenseDate.SelectedDate < trip.StartDate.Date || ExpenseDate.SelectedDate > trip.EndDate.Date)
        {
            ExpenseDate.SelectedDate = trip.StartDate.Date;
        }
    }

    private async void OnBudgetAction(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            var check21 = SelectedTripId();
            if (check21.IsFailure)
            {
                ShowValidationError(check21.Error, log: true);
                return;
            }

            int tripId = check21.Value!;
            if (UiInput.Action(sender) == "Expense")
            {
                var check22 = UiInput.Money(ExpenseAmount);
                if (check22.IsFailure)
                {
                    ShowValidationError(check22.Error, log: true);
                    return;
                }

                var check23 = UiInput.Date(ExpenseDate);
                if (check23.IsFailure)
                {
                    ShowValidationError(check23.Error, log: true);
                    return;
                }

                var check24 = await budget.AddExpenseAsync(tripId, ExpenseCategory.Text, ExpenseDescription.Text, check22.Value!, check23.Value!);
                if (check24.IsFailure)
                {
                    ShowValidationError(check24.Error, log: false);
                    return;
                }

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

    private async void OnPackingAction(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            switch (UiInput.Action(sender))
            {
                case "Add":
                    var check25 = SelectedTripId();
                    if (check25.IsFailure)
                    {
                        ShowValidationError(check25.Error, log: true);
                        return;
                    }

                    var check26 = await packing.AddAsync(check25.Value!, PackingName.Text);
                    if (check26.IsFailure)
                    {
                        ShowValidationError(check26.Error, log: false);
                        return;
                    }

                    PackingName.Clear();
                    break;
                case "Toggle":
                    var check27 = UiInput.Selected<PackingItem>(PackingGrid);
                    if (check27.IsFailure)
                    {
                        ShowValidationError(check27.Error, log: true);
                        return;
                    }

                    var check28 = await packing.ToggleAsync(check27.Value!.PackingItemId);
                    if (check28.IsFailure)
                    {
                        ShowValidationError(check28.Error, log: false);
                        return;
                    }

                    break;
                case "Delete":
                    var check29 = UiInput.Selected<PackingItem>(PackingGrid);
                    if (check29.IsFailure)
                    {
                        ShowValidationError(check29.Error, log: true);
                        return;
                    }

                    var check30 = await packing.DeleteAsync(check29.Value!.PackingItemId);
                    if (check30.IsFailure)
                    {
                        ShowValidationError(check30.Error, log: false);
                        return;
                    }

                    break;
            }

            await RefreshBudgetAsync();
        });
    }

    private bool Confirm(string message)
    {
        return MessageBox.Show(this, message, "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
