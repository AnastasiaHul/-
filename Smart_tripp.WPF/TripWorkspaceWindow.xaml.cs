using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;
public partial class TripWorkspaceWindow : Window
{
    private readonly Trip trip;
    private readonly TripService trips;
    private readonly ItineraryService itinerary;
    private readonly BudgetService budget;
    private readonly PackingService packing;
    private readonly MapRouteService mapRoutes;
    private readonly UserSession session;
    private readonly IAppLogger logger;
    private bool busy;
    private bool allowClose;
    private bool validationFailed;
    public string? RequestedAction { get; private set; }
    public int SelectedTabIndex => WorkspaceTabs.SelectedIndex;

    public TripWorkspaceWindow(Trip trip, TripService trips, ItineraryService itinerary, BudgetService budget, PackingService packing, MapRouteService mapRoutes, UserSession session, IAppLogger logger, int selectedTab = 0)
    {
        this.trip = trip;
        this.trips = trips;
        this.itinerary = itinerary;
        this.budget = budget;
        this.packing = packing;
        this.mapRoutes = mapRoutes;
        this.session = session;
        this.logger = logger;
        InitializeComponent();
        DataContext = trip;
        WorkspaceTabs.SelectedIndex = selectedTab;
        ExpenseDateBox.SelectedDate = trip.StartDate.Date;
        BackButton.Click += (_, _) => Close();
        EditTripButton.Click += (_, _) => Finish("EditTrip");
        BrowseCatalogButton.Click += (_, _) => Finish("Catalog");
        DeleteTripButton.Click += OnDeleteTrip;
        RemoveVisitButton.Click += OnRemoveVisit;
        SaveExpenseButton.Click += OnSaveExpense;
        AddPackingButton.Click += OnAddPacking;
        TogglePackingButton.Click += OnTogglePacking;
        DeletePackingButton.Click += OnDeletePacking;
        OpenRouteButton.Click += OnOpenRoute;
        Closing += (_, args) => args.Cancel = busy && !allowClose;
        Loaded += async (_, _) => await ExecuteAsync(RefreshAsync);
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        validationFailed = false;
        IsEnabled = false;
        WorkspaceStatusText.Text = "Виконується…";
        try
        {
            await action();
            if (!validationFailed)
            {
                WorkspaceStatusText.Text = "Готово. Дані подорожі оновлено.";
            }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "TripWorkspaceAction", session.CurrentUser?.UserId);
            string message = exception is ArgumentException or UnauthorizedAccessException ? exception.Message : "Не вдалося виконати дію. " + "Деталі збережено в журналі.";
            WorkspaceStatusText.Text = message;
            MessageBox.Show(this, message, "Smart Trip", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
            busy = false;
        }
    }

    private void ShowValidationError(string message, bool log = false)
    {
        validationFailed = true;
        WorkspaceStatusText.Text = message;
        if (log)
        {
            logger.Information($"TripWorkspace.ValidationFailed: {message}", session.CurrentUser?.UserId);
        }

        MessageBox.Show(this, message, "Перевірте введені дані", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private async Task RefreshAsync()
    {
        Result<User> access = session.TryRequireRole("Tourist");
        if (access.IsFailure)
        {
            ShowValidationError(access.Error, log: true);
            return;
        }

        var check1 = await budget.CalculateAsync(trip.TripId);
        if (check1.IsFailure)
        {
            ShowValidationError(check1.Error, log: false);
            return;
        }

        BudgetSummary summary = check1.Value!;
        PlannedText.Text = $"{summary.Planned:N2} ₴";
        SpentText.Text = $"{summary.Spent:N2} ₴";
        RemainingText.Text = $"{summary.Remaining:N2} ₴";
        RemainingText.Foreground = summary.Remaining < 0 ? new SolidColorBrush(Color.FromRgb(181, 71, 60)) : new SolidColorBrush(Color.FromRgb(22, 125, 110));
        var check2 = await itinerary.ListAsync(trip.TripId);
        if (check2.IsFailure)
        {
            ShowValidationError(check2.Error, log: false);
            return;
        }

        VisitsGrid.ItemsSource = check2.Value!;
        var check3 = await budget.ListAsync(trip.TripId);
        if (check3.IsFailure)
        {
            ShowValidationError(check3.Error, log: false);
            return;
        }

        TripExpensesGrid.ItemsSource = (check3.Value!).OrderByDescending(expense => expense.Date).ToList();
        var check4 = await budget.StatisticsAsync(trip.TripId);
        if (check4.IsFailure)
        {
            ShowValidationError(check4.Error, log: false);
            return;
        }

        CategoryStatisticsGrid.ItemsSource = check4.Value!;
        var check5 = await packing.ListAsync(trip.TripId);
        if (check5.IsFailure)
        {
            ShowValidationError(check5.Error, log: false);
            return;
        }

        TripPackingGrid.ItemsSource = check5.Value!;
    }

    private void Finish(string action)
    {
        if (busy)
        {
            return;
        }

        RequestedAction = action;
        allowClose = true;
        Close();
    }

    private async void OnDeleteTrip(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            MessageBoxResult answer = MessageBox.Show(this, $"Видалити подорож «{trip.Title}» " + "разом із планом відвідувань, " + "витратами, списком речей та бронюваннями?", "Видалення подорожі", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            Result result = await trips.DeleteAsync(trip.TripId);
            if (result.IsFailure)
            {
                ShowValidationError(result.Error);
                return;
            }

            RequestedAction = "Deleted";
            allowClose = true;
            Close();
        });
    }

    private async void OnRemoveVisit(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            var check6 = UiInput.Selected<ItineraryVisit>(VisitsGrid);
            if (check6.IsFailure)
            {
                ShowValidationError(check6.Error, log: true);
                return;
            }

            ItineraryVisit visit = check6.Value!;
            MessageBoxResult answer = MessageBox.Show(this, $"Прибрати «{visit.PlaceName}» " + "з плану відвідувань?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            var check7 = await itinerary.RemoveAsync(visit.ItineraryVisitId);
            if (check7.IsFailure)
            {
                ShowValidationError(check7.Error, log: false);
                return;
            }

            await RefreshAsync();
        });
    }

    private async void OnSaveExpense(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            var check8 = UiInput.Money(ExpenseAmountBox);
            if (check8.IsFailure)
            {
                ShowValidationError(check8.Error, log: true);
                return;
            }

            var check9 = UiInput.Date(ExpenseDateBox);
            if (check9.IsFailure)
            {
                ShowValidationError(check9.Error, log: true);
                return;
            }

            var check10 = await budget.AddExpenseAsync(trip.TripId, ExpenseCategoryBox.Text, ExpenseDescriptionBox.Text, check8.Value!, check9.Value!);
            if (check10.IsFailure)
            {
                ShowValidationError(check10.Error, log: false);
                return;
            }

            ExpenseAmountBox.Clear();
            ExpenseDescriptionBox.Clear();
            await RefreshAsync();
        });
    }

    private async void OnAddPacking(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            var check11 = await packing.AddAsync(trip.TripId, PackingNameBox.Text);
            if (check11.IsFailure)
            {
                ShowValidationError(check11.Error, log: false);
                return;
            }

            PackingNameBox.Clear();
            await RefreshAsync();
        });
    }

    private async void OnTogglePacking(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            var check12 = UiInput.Selected<PackingItem>(TripPackingGrid);
            if (check12.IsFailure)
            {
                ShowValidationError(check12.Error, log: true);
                return;
            }

            PackingItem item = check12.Value!;
            var check13 = await packing.ToggleAsync(item.PackingItemId);
            if (check13.IsFailure)
            {
                ShowValidationError(check13.Error, log: false);
                return;
            }

            await RefreshAsync();
        });
    }

    private async void OnDeletePacking(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            var check14 = UiInput.Selected<PackingItem>(TripPackingGrid);
            if (check14.IsFailure)
            {
                ShowValidationError(check14.Error, log: true);
                return;
            }

            PackingItem item = check14.Value!;
            MessageBoxResult answer = MessageBox.Show(this, $"Видалити річ «{item.Name}»?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            var check15 = await packing.DeleteAsync(item.PackingItemId);
            if (check15.IsFailure)
            {
                ShowValidationError(check15.Error, log: false);
                return;
            }

            await RefreshAsync();
        });
    }

    private async void OnOpenRoute(object sender, RoutedEventArgs e)
    {
        if (busy)
        {
            return;
        }

        var form = new StackPanel
        {
            Margin = new Thickness(24)
        };
        form.Children.Add(new TextBlock { Text = "Маршрут подорожі", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 20) });
        form.Children.Add(new TextBlock { Text = "День подорожі" });
        var dayBox = new DatePicker
        {
            DisplayDateStart = trip.StartDate.Date,
            DisplayDateEnd = trip.EndDate.Date,
            SelectedDate = trip.StartDate.Date,
            Margin = new Thickness(0, 8, 0, 16)
        };
        form.Children.Add(dayBox);
        form.Children.Add(new TextBlock { Text = "Спосіб пересування" });
        var modeBox = new ComboBox
        {
            Margin = new Thickness(0, 8, 0, 16)
        };
        modeBox.Items.Add(new ComboBoxItem { Content = "Пішки", Tag = "walking" });
        modeBox.Items.Add(new ComboBoxItem { Content = "Автомобілем", Tag = "driving" });
        modeBox.SelectedIndex = 0;
        form.Children.Add(modeBox);
        form.Children.Add(new TextBlock { Text = "Маршрут включатиме заплановані місця " + "вибраного дня в порядку часу. " + "Карта відкриється у браузері.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var buttons = new WrapPanel();
        var openButton = new Button
        {
            Content = "Показати маршрут"
        };
        var cancelButton = new Button
        {
            Content = "← Назад",
            Style = (Style)FindResource("SecondaryButton")
        };
        buttons.Children.Add(openButton);
        buttons.Children.Add(cancelButton);
        form.Children.Add(buttons);
        var dialog = new Window
        {
            Title = "Маршрут — Smart Trip",
            Owner = this,
            Width = 560,
            Height = 460,
            MinWidth = 440,
            MinHeight = 420,
            Background = Background,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new ScrollViewer
            {
                Content = form,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };
        openButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            if (modeBox.SelectedItem is not ComboBoxItem selectedMode || selectedMode.Tag is not string mode)
            {
                ShowValidationError("Виберіть спосіб пересування.", log: true);
                return;
            }

            var check16 = UiInput.Date(dayBox);
            if (check16.IsFailure)
            {
                ShowValidationError(check16.Error, log: true);
                return;
            }

            var check17 = await mapRoutes.BuildUrlAsync(trip.TripId, check16.Value!, mode);
            if (check17.IsFailure)
            {
                ShowValidationError(check17.Error, log: false);
                return;
            }

            string url = check17.Value!;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException("Не вдалося відкрити браузер. " + "Перевірте браузер за замовчуванням " + "у налаштуваннях Windows.", exception);
            }

            logger.Information("OpenTripRouteMap", session.CurrentUser?.UserId);
        });
    }
}
