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

namespace Smart_tripp.WPF;

public sealed class TripToolsWindow : TravelToolWindow
{
    private readonly Trip trip;
    private readonly TripTools tools;
    private readonly BudgetService budget;
    private readonly ItineraryService itinerary;
    private readonly PackingService packing;
    private readonly UserSession session;
    private readonly IAppLogger logger;

    private readonly DataGrid bookingsGrid = CreateGrid(
        ("Тип", nameof(TripBooking.KindName), 90),
        ("Деталі", nameof(TripBooking.Title), 0),
        ("Початок", nameof(TripBooking.StartsAt), 105),
        ("Сума ₴", nameof(TripBooking.Price), 90),
        ("Статус", nameof(TripBooking.StatusName), 120),
        ("У витратах", nameof(TripBooking.ExpenseState), 100));

    private readonly DataGrid expensesGrid = CreateGrid(
        ("Дата", nameof(Expense.Date), 105),
        ("Категорія", nameof(Expense.Category), 140),
        ("Опис", nameof(Expense.Description), 0),
        ("Сума ₴", nameof(Expense.Amount), 100));

    private readonly TextBox expenseCategory = new();
    private readonly TextBox expenseDescription = new();
    private readonly TextBox expenseAmount = new();
    private readonly DatePicker expenseDate = new();

    private readonly DataGrid visitsGrid = CreateGrid(
        ("Дата і час", nameof(ItineraryVisit.VisitAt), 155),
        ("Місце", nameof(ItineraryVisit.PlaceName), 0),
        ("Примітка", nameof(ItineraryVisit.Notes), 0));

    private readonly DatePicker visitDate = new();
    private readonly TextBox visitTime = new() { Text = "10:00" };
    private readonly TextBox visitNotes = new();

    private readonly TextBox copyTitle = new();
    private readonly DatePicker copyStart = new();

    private readonly ComboBox templateBox = new() { DisplayMemberPath = nameof(PackingTemplate.Name) };
    private readonly TextBlock templatePreview = new() { Margin = new Thickness(3, 8, 3, 8) };
    private readonly TextBlock packingList = new() { Margin = new Thickness(3, 8, 3, 8) };

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
        this.budget = budget;
        this.itinerary = itinerary;
        this.packing = packing;
        this.session = session;
        this.logger = logger;
        MinWidth = 860;

        foreach (DatePicker picker in new[] { expenseDate, visitDate, recommendationDate })
        {
            picker.DisplayDateStart = trip.StartDate.Date;
            picker.DisplayDateEnd = trip.EndDate.Date;
            picker.SelectedDate = trip.StartDate.Date;
        }

        copyTitle.Text = trip.Title.Length > 140 ? trip.Title : trip.Title + " (копія)";
        copyStart.DisplayDateStart = DateTime.Today;
        copyStart.SelectedDate = DateTime.Today.AddDays(7);

        var tabs = new TabControl();
        tabs.Items.Add(BookingsTab());
        tabs.Items.Add(ExpensesTab());
        tabs.Items.Add(VisitsTab());
        tabs.Items.Add(CopyTab());
        tabs.Items.Add(TemplatesTab());
        tabs.Items.Add(RecommendationsTab());
        tabs.Items.Add(ReportTab());
        Form.Children.Add(tabs);

        Loaded += async (_, _) => await RunAsync("TripTools.Open", RefreshAsync);
    }

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

    // 3. Редагування та видалення витрати.
    private TabItem ExpensesTab()
    {
        var panel = CreatePage("Виберіть витрату, щоб змінити або видалити її.");
        panel.Children.Add(expensesGrid);
        expensesGrid.SelectionChanged += (_, _) =>
        {
            if (expensesGrid.SelectedItem is not Expense expense)
            {
                return;
            }

            expenseCategory.Text = expense.Category;
            expenseDescription.Text = expense.Description;
            expenseAmount.Text = expense.Amount.ToString("0.##", CultureInfo.CurrentCulture);
            expenseDate.SelectedDate = expense.Date.Date;
        };
        AddField(panel, "Категорія", expenseCategory);
        AddField(panel, "Опис", expenseDescription);
        AddField(panel, "Сума ₴", expenseAmount);
        AddField(panel, "Дата", expenseDate);

        var buttons = new WrapPanel();
        buttons.Children.Add(ActionButton("Зберегти зміни", "Expense.Edit", async () =>
        {
            if (expensesGrid.SelectedItem is not Expense expense)
            {
                ShowStatus("Спочатку виберіть витрату.");
                return;
            }

            var amount = UiInput.Money(expenseAmount);
            var date = UiInput.Date(expenseDate);
            if (amount.IsFailure || date.IsFailure)
            {
                Reject("Expense.Edit", amount.IsFailure ? amount.Error : date.Error);
                return;
            }

            var result = await tools.Expenses.UpdateAsync(expense.ExpenseId, expenseCategory.Text,
                expenseDescription.Text, amount.Value, date.Value);
            if (result.IsFailure)
            {
                ShowStatus(result.Error);
                return;
            }

            await RefreshAsync();
            ShowStatus("Витрату оновлено.");
        }));
        buttons.Children.Add(ActionButton("Видалити витрату", "Expense.Delete", async () =>
        {
            if (expensesGrid.SelectedItem is not Expense expense)
            {
                ShowStatus("Спочатку виберіть витрату.");
                return;
            }

            if (!Ask($"Видалити витрату «{expense.Description}»?"))
            {
                return;
            }

            var result = await tools.Expenses.DeleteAsync(expense.ExpenseId);
            if (result.IsFailure)
            {
                ShowStatus(result.Error);
                return;
            }

            expenseCategory.Clear();
            expenseDescription.Clear();
            expenseAmount.Clear();
            await RefreshAsync();
            ShowStatus("Витрату видалено.");
        }));
        panel.Children.Add(buttons);
        return CreateTab("Витрати", panel);
    }

    // 4. Перенесення запланованого відвідування.
    private TabItem VisitsTab()
    {
        var panel = CreatePage("Виберіть відвідування та вкажіть новий час або примітку.");
        panel.Children.Add(visitsGrid);
        visitsGrid.SelectionChanged += (_, _) =>
        {
            if (visitsGrid.SelectedItem is not ItineraryVisit visit)
            {
                return;
            }

            visitDate.SelectedDate = visit.VisitAt.Date;
            visitTime.Text = visit.VisitAt.ToString("HH:mm", CultureInfo.InvariantCulture);
            visitNotes.Text = visit.Notes;
        };
        AddField(panel, "Нова дата", visitDate);
        AddField(panel, "Новий час (ГГ:ХХ)", visitTime);
        AddField(panel, "Примітка", visitNotes);
        panel.Children.Add(ActionButton("Перенести відвідування", "Itinerary.Reschedule", async () =>
        {
            if (visitsGrid.SelectedItem is not ItineraryVisit visit)
            {
                ShowStatus("Спочатку виберіть відвідування.");
                return;
            }

            if (ReadDateTime("Itinerary.Reschedule", visitDate, visitTime) is not DateTime time)
            {
                return;
            }

            var result = await tools.Visits.RescheduleAsync(visit.ItineraryVisitId, time, visitNotes.Text);
            if (result.IsFailure)
            {
                ShowStatus(result.Error);
                return;
            }

            await RefreshAsync();
            ShowStatus("Відвідування перенесено.");
        }));
        return CreateTab("Перенесення", panel);
    }

    // 5. Копіювання подорожі.
    private TabItem CopyTab()
    {
        var panel = CreatePage("Створює нову подорож із тими самими параметрами, планом і списком речей. "
            + "Дати плану зсуваються. Витрати та бронювання не копіюються.");
        AddField(panel, "Назва нової подорожі", copyTitle);
        AddField(panel, "Нова дата початку", copyStart);
        panel.Children.Add(ActionButton("Створити копію", "Trip.Copy", async () =>
        {
            var date = UiInput.Date(copyStart);
            if (date.IsFailure)
            {
                Reject("Trip.Copy", date.Error);
                return;
            }

            var result = await tools.Copies.CopyAsync(trip.TripId, copyTitle.Text, date.Value);
            if (result.IsFailure)
            {
                ShowStatus(result.Error);
                return;
            }

            TripCopyResult copy = result.Value!;
            ShowStatus($"Створено «{copy.Trip.Title}»: відвідувань — {copy.Visits}, речей — {copy.PackingItems}"
                + (copy.SkippedVisits > 0 ? $", пропущено недоступних місць — {copy.SkippedVisits}" : string.Empty)
                + ". Вона з’явиться у «Моїх подорожах».");
        }));
        return CreateTab("Копія", panel);
    }

    // 6. Шаблони списку речей.
    private TabItem TemplatesTab()
    {
        var panel = CreatePage("Додайте готовий набір речей. Дублікати пропускаються.");
        templateBox.SelectionChanged += (_, _) => templatePreview.Text =
            templateBox.SelectedItem is PackingTemplate template
                ? "У шаблоні: " + string.Join(", ", template.Items)
                : string.Empty;
        templateBox.ItemsSource = PackingTemplateService.Templates;
        templateBox.SelectedIndex = 0;
        AddField(panel, "Шаблон", templateBox);
        panel.Children.Add(templatePreview);
        panel.Children.Add(ActionButton("Додати речі з шаблону", "Packing.ApplyTemplate", async () =>
        {
            string name = (templateBox.SelectedItem as PackingTemplate)?.Name ?? string.Empty;
            var result = await tools.Templates.ApplyAsync(trip.TripId, name);
            if (result.IsFailure)
            {
                ShowStatus(result.Error);
                return;
            }

            await RefreshAsync();
            ShowStatus($"Додано речей: {result.Value}.");
        }));
        panel.Children.Add(packingList);
        return CreateTab("Шаблони речей", panel);
    }

    // 7. Рекомендації місць.
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

    // 8. Експорт звіту.
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
        var expenses = await budget.ListAsync(trip.TripId);
        var visits = await itinerary.ListAsync(trip.TripId);
        var items = await packing.ListAsync(trip.TripId);
        var recommendations = await tools.Recommendations.GetAsync(trip.TripId);
        if (bookings.IsFailure || expenses.IsFailure || visits.IsFailure
            || items.IsFailure || recommendations.IsFailure)
        {
            ShowStatus("Не вдалося завантажити дані. Увійдіть як власник подорожі.");
            return;
        }

        bookingsGrid.ItemsSource = bookings.Value;
        expensesGrid.ItemsSource = expenses.Value!.OrderByDescending(x => x.Date).ToList();
        visitsGrid.ItemsSource = visits.Value;
        recommendationsGrid.ItemsSource = recommendations.Value;
        packingList.Text = items.Value!.Count == 0
            ? "Список речей поки порожній."
            : "Уже у списку: " + string.Join(", ", items.Value.Select(x => x.Name));
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

    // Ширина 0 означає, що колонка займає вільне місце.
    private static DataGrid CreateGrid(params (string Header, string Path, double Width)[] columns)
    {
        var grid = new DataGrid { Height = 230 };
        foreach ((string header, string path, double width) in columns)
        {
            var binding = new Binding(path);
            if (path is nameof(Expense.Date) or nameof(TripBooking.StartsAt))
            {
                binding.StringFormat = "dd.MM.yyyy";
            }
            else if (path == nameof(ItineraryVisit.VisitAt))
            {
                binding.StringFormat = "dd.MM.yyyy HH:mm";
            }
            else if (path is nameof(Expense.Amount) or nameof(TripBooking.Price))
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
