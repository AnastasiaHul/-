using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using Smart_tripp.WPF.ViewModels;
using Smart_tripp.WPF.Views;

namespace Smart_tripp.WPF;

public sealed class TripToolsWindow : TravelToolWindow, IToolHost
{
    private readonly Trip trip;
    private readonly TripTools tools;
    private readonly ItineraryService itinerary;
    private readonly UserSession session;
    private readonly IAppLogger logger;

    private readonly DataGrid bookingsGrid = CreateGrid(
        ("Тип", nameof(TripBooking.KindName), 90),
        ("Деталі", nameof(TripBooking.Title), 0),
        ("Початок", nameof(TripBooking.StartsAt), 105),
        ("Сума ₴", nameof(TripBooking.Price), 90),
        ("Статус", nameof(TripBooking.StatusName), 120),
        ("У витратах", nameof(TripBooking.ExpenseState), 100));

    // реалізовані за MVVM: дані та команди містяться у ViewModel.
    private readonly ExpenseEditViewModel expenseEdit;
    private readonly RescheduleVisitViewModel reschedule;
    private readonly TripCopyViewModel tripCopy;
    private readonly PackingTemplateViewModel packingTemplates;

    private readonly DataGrid recommendationsGrid = CreateGrid(
        ("Місце", nameof(PlaceRecommendation.Name), 0),
        ("Категорія", nameof(PlaceRecommendation.Category), 120),
        ("Ціна ₴", nameof(PlaceRecommendation.Price), 85),
        ("Оцінка", nameof(PlaceRecommendation.Rating), 75),
        ("Адреса", nameof(PlaceRecommendation.Address), 0),
        ("Чому радимо", nameof(PlaceRecommendation.Reason), 0));

    private readonly DatePicker recommendationDate = new();
    private readonly TextBox recommendationTime = new() { Text = "10:00" };

    internal TripToolsWindow(Trip trip, TripTools tools, BudgetService budget,
        ItineraryService itinerary, PackingService packing, UserSession session, IAppLogger logger)
        : base("Інструменти подорожі", 1060, Math.Min(780, SystemParameters.WorkArea.Height - 40), session, logger)
    {
        this.trip = trip;
        this.tools = tools;
        this.itinerary = itinerary;
        this.session = session;
        this.logger = logger;
        MinWidth = 860;

        recommendationDate.DisplayDateStart = trip.StartDate.Date;
        recommendationDate.DisplayDateEnd = trip.EndDate.Date;
        recommendationDate.SelectedDate = trip.StartDate.Date;

        expenseEdit = new ExpenseEditViewModel(trip, budget, tools.Expenses, this);
        reschedule = new RescheduleVisitViewModel(trip, itinerary, tools.Visits, this);
        tripCopy = new TripCopyViewModel(trip, tools.Copies, this);
        packingTemplates = new PackingTemplateViewModel(trip, packing, tools.Templates, this);

        var tabs = new TabControl();
        tabs.Items.Add(BookingsTab());
        tabs.Items.Add(CreateTab("Витрати", new ExpenseEditView { DataContext = expenseEdit }));
        tabs.Items.Add(CreateTab("Перенесення", new RescheduleVisitView { DataContext = reschedule }));
        tabs.Items.Add(CreateTab("Копія", new TripCopyView { DataContext = tripCopy }));
        tabs.Items.Add(CreateTab("Шаблони речей", new PackingTemplateView { DataContext = packingTemplates }));
        tabs.Items.Add(RecommendationsTab());
        tabs.Items.Add(ReportTab());
        Form.Children.Add(tabs);

        Loaded += async (_, _) => await RunAsync("TripTools.Open", RefreshAsync);
    }

    Task IToolHost.RunAsync(string action, Func<Task> work)
    {
        ShowStatus(string.Empty);
        return RunAsync(action, work);
    }

    void IToolHost.Reject(string action, string message) => Reject(action, message);

    bool IToolHost.Confirm(string message) => Ask(message);

    Task IToolHost.RefreshAsync() => RefreshAsync();

    // 1. Скасування бронювання та 2. перенесення бронювань у витрати.
    private TabItem BookingsTab()
    {
        var panel = CreatePage("Бронювання подорожі. Скасувати можна ті, що ще не почалися.");
        panel.Children.Add(bookingsGrid);
        var buttons = new WrapPanel();
        buttons.Children.Add(ActionButton("Скасувати вибране бронювання", "Booking.CancelOwn", CancelBookingAsync));
        buttons.Children.Add(ActionButton("Додати бронювання у витрати", "Booking.AddToExpenses", async () =>
        {
            var result = await tools.Bookings.AddToExpensesAsync(trip.TripId);
            if (result.IsFailure)
            {
                ShowStatus(result.Error);
                return;
            }

            await RefreshAsync();
            ShowStatus(result.Value == 0
                ? "Нових бронювань для додавання немає."
                : $"Додано витрат: {result.Value}.");
        }));
        panel.Children.Add(buttons);
        return CreateTab("Бронювання", panel);
    }

    private async Task CancelBookingAsync()
    {
        if (bookingsGrid.SelectedItem is not TripBooking booking)
        {
            ShowStatus("Спочатку виберіть бронювання в таблиці.");
            return;
        }

        if (!Ask($"Скасувати бронювання «{booking.Title}»? Пов’язану витрату також буде прибрано."))
        {
            return;
        }

        var result = await tools.Bookings.CancelAsync(trip.TripId, booking.Kind, booking.ReservationId);
        if (result.IsFailure)
        {
            ShowStatus(result.Error);
            return;
        }

        await RefreshAsync();
        ShowStatus("Бронювання скасовано.");
    }

    // Рекомендації місць
    private TabItem RecommendationsTab()
    {
        var panel = CreatePage($"Місця з міста «{trip.City}» (за адресою), які ще не в плані та не дорожчі "
            + "за залишок бюджету. Спочатку — ті, що відповідають інтересам.");
        panel.Children.Add(recommendationsGrid);
        AddField(panel, "Дата відвідування", recommendationDate);
        AddField(panel, "Час (ГГ:ХХ)", recommendationTime);
        panel.Children.Add(ActionButton("Додати вибране місце в план", "Place.AddRecommended", async () =>
        {
            if (recommendationsGrid.SelectedItem is not PlaceRecommendation place)
            {
                ShowStatus("Спочатку виберіть місце.");
                return;
            }

            if (ReadDateTime("Place.AddRecommended", recommendationDate, recommendationTime) is not DateTime time)
            {
                return;
            }

            var result = await itinerary.AddAsync(trip.TripId, place.PlaceId, time, string.Empty);
            if (result.IsFailure)
            {
                ShowStatus(result.Error);
                return;
            }

            await RefreshAsync();
            ShowStatus($"«{place.Name}» додано в план.");
        }));
        return CreateTab("Рекомендації", panel);
    }

    //  Експорт звіту
    private TabItem ReportTab()
    {
        var panel = CreatePage("Звіт містить бюджет, план, бронювання, витрати та речі. "
            + "Файл HTML відкривається у браузері, звідки його можна надрукувати або зберегти в PDF.");
        panel.Children.Add(ActionButton("Зберегти звіт", "Trip.Report", SaveReportAsync));
        return CreateTab("Звіт", panel);
    }

    private async Task SaveReportAsync()
    {
        var result = await tools.Reports.BuildHtmlAsync(trip.TripId);
        if (result.IsFailure)
        {
            ShowStatus(result.Error);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Сторінка HTML (*.html)|*.html",
            DefaultExt = ".html",
            FileName = $"smart-trip-{trip.StartDate:yyyy-MM-dd}.html"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, result.Value!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Error(exception, "Trip.Report.Save", session.CurrentUser?.UserId);
            ShowStatus("Не вдалося зберегти файл. Виберіть інше місце або закрийте цей файл.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true });
            ShowStatus("Звіт збережено та відкрито.");
        }
        catch (Win32Exception exception)
        {
            logger.Error(exception, "Trip.Report.Open", session.CurrentUser?.UserId);
            ShowStatus("Звіт збережено, але відкрити його автоматично не вдалося.");
        }
    }

    private async Task RefreshAsync()
    {
        var bookings = await tools.Bookings.ListAsync(trip.TripId);
        var recommendations = await tools.Recommendations.GetAsync(trip.TripId);
        bool loaded = await expenseEdit.LoadAsync();
        loaded &= await reschedule.LoadAsync();
        loaded &= await packingTemplates.LoadAsync();
        if (bookings.IsFailure || recommendations.IsFailure || !loaded)
        {
            ShowStatus("Не вдалося завантажити дані. Увійдіть як власник подорожі.");
            return;
        }

        bookingsGrid.ItemsSource = bookings.Value;
        recommendationsGrid.ItemsSource = recommendations.Value;
    }

    private DateTime? ReadDateTime(string action, DatePicker picker, TextBox timeBox)
    {
        var date = UiInput.Date(picker);
        if (date.IsFailure)
        {
            Reject(action, date.Error);
            return null;
        }

        if (!TimeSpan.TryParseExact(timeBox.Text.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out TimeSpan time))
        {
            Reject(action, "Введіть час у форматі ГГ:ХХ, наприклад 14:30.");
            return null;
        }

        return date.Value.Date + time;
    }

    private bool Ask(string message) =>
        MessageBox.Show(this, message, "Підтвердження", MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

    private Button ActionButton(string title, string action, Func<Task> run)
    {
        Button button = MakeButton(title);
        button.Click += async (_, _) => await RunAsync(action, run);
        return button;
    }

    private static StackPanel CreatePage(string hint)
    {
        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = hint, Margin = new Thickness(3, 0, 3, 4) });
        return panel;
    }

    private static TabItem CreateTab(string title, UIElement content) =>
        new() { Header = title, Content = content, Padding = new Thickness(12, 7, 12, 7) };

    //  0 означає що колонка займає вільне місце.
    private static DataGrid CreateGrid(params (string Header, string Path, double Width)[] columns)
    {
        var grid = new DataGrid { Height = 230 };
        foreach ((string header, string path, double width) in columns)
        {
            var binding = new Binding(path);
            if (path == nameof(TripBooking.StartsAt))
            {
                binding.StringFormat = "dd.MM.yyyy";
            }
            else if (path == nameof(TripBooking.Price))
            {
                binding.StringFormat = "N2";
            }

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = binding,
                Width = width > 0 ? new DataGridLength(width) : new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }

        return grid;
    }
}
