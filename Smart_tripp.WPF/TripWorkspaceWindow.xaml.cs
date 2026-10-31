using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    public string? RequestedAction { get; private set; }

    public int SelectedTabIndex =>
        WorkspaceTabs.SelectedIndex;

    public TripWorkspaceWindow(
        Trip trip,
        TripService trips,
        ItineraryService itinerary,
        BudgetService budget,
        PackingService packing,
        MapRouteService mapRoutes,
        UserSession session,
        IAppLogger logger,
        int selectedTab = 0)
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

        EditTripButton.Click += (_, _) =>
            Finish("EditTrip");

        BrowseCatalogButton.Click += (_, _) =>
            Finish("Catalog");

        DeleteTripButton.Click += OnDeleteTrip;
        RemoveVisitButton.Click += OnRemoveVisit;
        SaveExpenseButton.Click += OnSaveExpense;
        AddPackingButton.Click += OnAddPacking;
        TogglePackingButton.Click += OnTogglePacking;
        DeletePackingButton.Click += OnDeletePacking;
        OpenRouteButton.Click += OnOpenRoute;

        Closing += (_, args) =>
            args.Cancel = busy && !allowClose;

        Loaded += async (_, _) =>
            await ExecuteAsync(RefreshAsync);
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        IsEnabled = false;
        WorkspaceStatusText.Text = "Виконується…";

        try
        {
            await action();

            WorkspaceStatusText.Text =
                "Готово. Дані подорожі оновлено.";
        }
        catch (Exception exception)
        {
            logger.Error(
                exception,
                "TripWorkspaceAction",
                session.CurrentUser?.UserId);

            string message =
                exception is ArgumentException
                    or UnauthorizedAccessException
                    ? exception.Message
                    : "Не вдалося виконати дію. "
                        + "Деталі збережено в журналі.";

            WorkspaceStatusText.Text = message;

            MessageBox.Show(
                this,
                message,
                "Smart Trip",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
            busy = false;
        }
    }

    private async Task RefreshAsync()
    {
        BudgetSummary summary =
            await budget.CalculateAsync(trip.TripId);

        PlannedText.Text = $"{summary.Planned:N2} ₴";
        SpentText.Text = $"{summary.Spent:N2} ₴";
        RemainingText.Text = $"{summary.Remaining:N2} ₴";

        RemainingText.Foreground = summary.Remaining < 0
            ? new SolidColorBrush(Color.FromRgb(181, 71, 60))
            : new SolidColorBrush(Color.FromRgb(22, 125, 110));

        VisitsGrid.ItemsSource =
            await itinerary.ListAsync(trip.TripId);

        TripExpensesGrid.ItemsSource =
            (await budget.ListAsync(trip.TripId))
                .OrderByDescending(expense => expense.Date)
                .ToList();

        CategoryStatisticsGrid.ItemsSource =
            await budget.StatisticsAsync(trip.TripId);

        TripPackingGrid.ItemsSource =
            await packing.ListAsync(trip.TripId);
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

    private async void OnDeleteTrip(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            MessageBoxResult answer = MessageBox.Show(
                this,
                $"Видалити подорож «{trip.Title}» "
                + "разом із планом відвідувань, "
                + "витратами та списком речей?",
                "Видалення подорожі",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            await trips.DeleteAsync(trip.TripId);

            RequestedAction = "Deleted";
            allowClose = true;
            Close();
        });
    }

    private async void OnRemoveVisit(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            ItineraryVisit visit =
                UiInput.Selected<ItineraryVisit>(VisitsGrid);

            MessageBoxResult answer = MessageBox.Show(
                this,
                $"Прибрати «{visit.PlaceName}» "
                + "з плану відвідувань?",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            await itinerary.RemoveAsync(
                visit.ItineraryVisitId);

            await RefreshAsync();
        });
    }

    private async void OnSaveExpense(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            await budget.AddExpenseAsync(
                trip.TripId,
                ExpenseCategoryBox.Text,
                ExpenseDescriptionBox.Text,
                UiInput.Money(ExpenseAmountBox),
                UiInput.Date(ExpenseDateBox));

            // Після успішного запису очищаємо суму:
            // повторне натискання не збереже ту саму форму.
            ExpenseAmountBox.Clear();
            ExpenseDescriptionBox.Clear();

            await RefreshAsync();
        });
    }

    private async void OnAddPacking(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            await packing.AddAsync(
                trip.TripId,
                PackingNameBox.Text);

            PackingNameBox.Clear();

            await RefreshAsync();
        });
    }

    private async void OnTogglePacking(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            PackingItem item =
                UiInput.Selected<PackingItem>(
                    TripPackingGrid);

            await packing.ToggleAsync(
                item.PackingItemId);

            await RefreshAsync();
        });
    }

    private async void OnDeletePacking(
        object sender,
        RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            PackingItem item =
                UiInput.Selected<PackingItem>(
                    TripPackingGrid);

            MessageBoxResult answer = MessageBox.Show(
                this,
                $"Видалити річ «{item.Name}»?",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            await packing.DeleteAsync(
                item.PackingItemId);

            await RefreshAsync();
        });
    }

    private async void OnOpenRoute(
        object sender,
        RoutedEventArgs e)
    {
        if (busy)
        {
            return;
        }

        var form = new StackPanel
        {
            Margin = new Thickness(24)
        };

        form.Children.Add(new TextBlock
        {
            Text = "Маршрут подорожі",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 20)
        });

        form.Children.Add(new TextBlock
        {
            Text = "День подорожі"
        });

        var dayBox = new DatePicker
        {
            DisplayDateStart = trip.StartDate.Date,
            DisplayDateEnd = trip.EndDate.Date,
            SelectedDate = trip.StartDate.Date,
            Margin = new Thickness(0, 8, 0, 16)
        };

        form.Children.Add(dayBox);

        form.Children.Add(new TextBlock
        {
            Text = "Спосіб пересування"
        });

        var modeBox = new ComboBox
        {
            Margin = new Thickness(0, 8, 0, 16)
        };

        modeBox.Items.Add(new ComboBoxItem
        {
            Content = "Пішки",
            Tag = "walking"
        });

        modeBox.Items.Add(new ComboBoxItem
        {
            Content = "Автомобілем",
            Tag = "driving"
        });

        modeBox.SelectedIndex = 0;

        form.Children.Add(modeBox);

        form.Children.Add(new TextBlock
        {
            Text = "Маршрут включатиме заплановані місця "
                + "вибраного дня в порядку часу. "
                + "Карта відкриється у браузері.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

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
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,

            Content = new ScrollViewer
            {
                Content = form,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto
            }
        };

        openButton.Click += (_, _) =>
            dialog.DialogResult = true;

        cancelButton.Click += (_, _) =>
            dialog.DialogResult = false;

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            if (modeBox.SelectedItem
                is not ComboBoxItem selectedMode
                || selectedMode.Tag is not string mode)
            {
                throw new ArgumentException(
                    "Виберіть спосіб пересування.");
            }

            string url = await mapRoutes.BuildUrlAsync(
                trip.TripId,
                UiInput.Date(dayBox),
                mode);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Win32Exception exception)
            {
                throw new ArgumentException(
                    "Не вдалося відкрити браузер. "
                    + "Перевірте браузер за замовчуванням "
                    + "у налаштуваннях Windows.",
                    exception);
            }

            logger.Information(
                "OpenTripRouteMap",
                session.CurrentUser?.UserId);
        });
    }
}