using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;

namespace Smart_tripp.WPF;

public sealed class AdministrationWindow : TravelToolWindow
{
    private readonly AdministrationService administration;
    private readonly DataGrid places = MakeGrid();
    private readonly TextBox placeReason = new() { MaxLength = 300 };
    private readonly TextBox email = new() { MaxLength = 254 };
    private readonly TextBox accountReason = new() { MaxLength = 300 };
    private readonly TextBlock accountText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox bookingCode = new() { MaxLength = 80 };
    private readonly TextBox cancellationReason = new() { MaxLength = 300 };
    private readonly TextBlock bookingText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DatePicker fromDate = new();
    private readonly DatePicker untilDate = new();
    private readonly ComboBox actions = new();
    private readonly DataGrid audit = MakeGrid();
    private AccountSummary? account;
    private ExcursionSummary? excursion;

    public AdministrationWindow(AdministrationService administration, UserSession session, IAppLogger logger)
        : base("Адміністрування", 1020, 680, session, logger)
    {
        this.administration = administration;
        var tabs = new TabControl();
        tabs.Items.Add(PlaceTab());
        tabs.Items.Add(AccountTab());
        tabs.Items.Add(ExcursionTab());
        tabs.Items.Add(AuditTab());
        Form.Children.Add(tabs);
        Loaded += async (_, _) => await RunAsync("Admin.Place.List", LoadPendingAsync);
    }

    private TabItem PlaceTab()
    {
        var panel = MakePanel();
        AddColumn(places, "Назва", nameof(PendingPlace.Name));
        AddColumn(places, "Категорія", nameof(PendingPlace.Category));
        AddColumn(places, "Адреса", nameof(PendingPlace.Address));
        AddColumn(places, "Опис", nameof(PendingPlace.Description));
        AddColumn(places, "ID партнера", nameof(PendingPlace.PartnerId));
        AddButton(panel, "Оновити чергу", "Admin.Place.List", LoadPendingAsync);
        AddField(panel, "Місця, які очікують перевірки", places);
        AddField(panel, "Причина рішення (обов’язково)", placeReason);
        var buttons = new WrapPanel();
        AddButton(buttons, "Схвалити", AdministrationService.ApprovePlace, () => ReviewPlaceAsync(true));
        AddButton(buttons, "Повернути на доопрацювання", AdministrationService.ReturnPlace, () => ReviewPlaceAsync(false));
        panel.Children.Add(buttons);
        return new TabItem { Header = "Модерація місць", Content = panel };
    }

    private TabItem AccountTab()
    {
        var panel = MakePanel();
        AddField(panel, "Email акаунта", email);
        email.TextChanged += (_, _) => ShowAccount(null);
        AddButton(panel, "Знайти акаунт", "Admin.Account.Find", FindAccountAsync);
        panel.Children.Add(accountText);
        AddField(panel, "Причина блокування або розблокування", accountReason);
        var buttons = new WrapPanel();
        AddButton(buttons, "Заблокувати", AdministrationService.BlockAccount, () => SetAccountBlockedAsync(true));
        AddButton(buttons, "Розблокувати", AdministrationService.UnblockAccount, () => SetAccountBlockedAsync(false));
        panel.Children.Add(buttons);
        return new TabItem { Header = "Акаунти", Content = panel };
    }

    private TabItem ExcursionTab()
    {
        var panel = MakePanel();
        AddField(panel, "Код бронювання EX-…", bookingCode);
        bookingCode.TextChanged += (_, _) => ShowExcursion(null);
        AddButton(panel, "Знайти бронювання", "Admin.Excursion.Find", FindExcursionAsync);
        panel.Children.Add(bookingText);
        AddField(panel, "Причина скасування", cancellationReason);
        AddButton(panel, "Скасувати бронювання", AdministrationService.CancelExcursion, CancelExcursionAsync);
        return new TabItem { Header = "Бронювання екскурсій", Content = panel };
    }

    private TabItem AuditTab()
    {
        var panel = MakePanel();
        var filters = new WrapPanel();
        AddFilter(filters, "Від", fromDate);
        AddFilter(filters, "До (включно)", untilDate);
        actions.Items.Add(new ComboBoxItem { Content = "Усі дії" });
        AddAction("Схвалення місця", AdministrationService.ApprovePlace);
        AddAction("Повернення місця", AdministrationService.ReturnPlace);
        AddAction("Блокування", AdministrationService.BlockAccount);
        AddAction("Розблокування", AdministrationService.UnblockAccount);
        AddAction("Скасування екскурсії", AdministrationService.CancelExcursion);
        actions.SelectedIndex = 0;
        AddFilter(filters, "Тип дії", actions);
        panel.Children.Add(filters);
        AddButton(panel, "Показати журнал", AdministrationService.ViewAudit, LoadAuditAsync);
        AddColumn(audit, "Час (UTC)", "OccurredAtUtc", "dd.MM.yyyy HH:mm:ss");
        AddColumn(audit, "ID адміністратора", "ActorId");
        AddColumn(audit, "Дія", "Action");
        AddColumn(audit, "ID запису", "TargetId");
        AddColumn(audit, "Причина", "Reason");
        AddField(panel, "До 200 останніх змін за вибраним фільтром", audit);
        return new TabItem { Header = "Журнал дій", Content = panel };
    }

    private async Task LoadPendingAsync()
    {
        var result = await administration.ListPendingAsync();
        if (ShowFailure(result))
        {
            places.ItemsSource = null;
            return;
        }

        places.ItemsSource = result.Value;
        ShowStatus($"Місць у черзі: {result.Value!.Count}.");
    }

    private async Task ReviewPlaceAsync(bool approve)
    {
        int id = (places.SelectedItem as PendingPlace)?.PlaceId ?? 0;
        var result = await administration.ReviewPlaceAsync(id, approve, placeReason.Text);
        if (ShowFailure(result))
        {
            return;
        }

        placeReason.Clear();
        await LoadPendingAsync();
        ShowStatus(approve ? "Місце опубліковано." : "Місце повернуто партнеру на доопрацювання.");
    }

    private async Task FindAccountAsync()
    {
        var result = await administration.FindAccountAsync(email.Text);
        ShowAccount(result.Value);
        if (!ShowFailure(result))
        {
            ShowStatus("Акаунт знайдено.");
        }
    }

    private async Task SetAccountBlockedAsync(bool block)
    {
        var result = await administration.SetAccountBlockedAsync(account?.UserId ?? 0, block, accountReason.Text);
        if (ShowFailure(result))
        {
            return;
        }

        ShowAccount(account! with { IsBlocked = block });
        accountReason.Clear();
        ShowStatus(block ? "Нові входи в акаунт заблоковано." : "Вхід в акаунт відновлено.");
    }

    private async Task FindExcursionAsync()
    {
        var result = await administration.FindExcursionAsync(bookingCode.Text);
        ShowExcursion(result.Value);
        if (!ShowFailure(result))
        {
            ShowStatus("Бронювання знайдено.");
        }
    }

    private async Task CancelExcursionAsync()
    {
        if (excursion?.Status == "Confirmed" && !string.IsNullOrWhiteSpace(cancellationReason.Text)
            && MessageBox.Show(this, "Скасувати це бронювання?", "Підтвердження",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await administration.CancelExcursionAsync(excursion?.ReservationId ?? 0, cancellationReason.Text);
        if (ShowFailure(result))
        {
            return;
        }

        ShowExcursion(excursion! with { Status = "Cancelled", CancellationReason = cancellationReason.Text.Trim() });
        cancellationReason.Clear();
        ShowStatus("Бронювання скасовано.");
    }

    private async Task LoadAuditAsync()
    {
        var result = await administration.ListAuditAsync(fromDate.SelectedDate, untilDate.SelectedDate,
            (actions.SelectedItem as ComboBoxItem)?.Tag as string);
        audit.ItemsSource = result.Value;
        if (!ShowFailure(result))
        {
            ShowStatus($"Записів знайдено: {result.Value!.Count}.");
        }
    }

    private bool ShowFailure<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            ShowStatus(result.Error);
        }

        return result.IsFailure;
    }

    private void ShowAccount(AccountSummary? value)
    {
        account = value;
        accountText.Text = value is null ? "" : $"{value.Email} · {value.Role} · "
            + (value.IsBlocked ? "Заблоковано" : "Активний");
    }

    private void ShowExcursion(ExcursionSummary? value)
    {
        excursion = value;
        bookingText.Text = value is null ? "" : $"{value.BookingCode} · Подорож №{value.TripId} · "
            + (value.Status == "Confirmed" ? "Підтверджено" : "Скасовано")
            + (value.CancellationReason is null ? "" : $". Причина: {value.CancellationReason}");
    }

    private void AddButton(Panel panel, string title, string action, Func<Task> run)
    {
        var button = MakeButton(title);
        button.Margin = new Thickness(0, 8, 8, 4);
        button.Click += async (_, _) => await RunAsync(action, run);
        panel.Children.Add(button);
    }

    private void AddAction(string label, string action) =>
        actions.Items.Add(new ComboBoxItem { Content = label, Tag = action });

    private static void AddFilter(Panel filters, string label, Control control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        control.MinWidth = 140;
        AddField(panel, label, control);
        filters.Children.Add(panel);
    }

    private static StackPanel MakePanel() => new() { Margin = new Thickness(14) };

    private static DataGrid MakeGrid() => new()
    {
        AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
        Height = 280, SelectionMode = DataGridSelectionMode.Single
    };

    private static void AddColumn(DataGrid grid, string title, string path, string? format = null)
    {
        var binding = new Binding(path);
        if (format is not null)
        {
            binding.StringFormat = format;
        }

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = title, Binding = binding, Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
    }
}
