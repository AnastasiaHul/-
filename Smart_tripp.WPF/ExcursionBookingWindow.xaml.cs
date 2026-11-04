using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace Smart_tripp.WPF;
public partial class ExcursionBookingWindow : Window
{
    private readonly ExcursionBookingService booking;
    private readonly TripService trips;
    private readonly UserSession session;
    private readonly IAppLogger logger;
    private bool busy;
    private bool validationFailed;
    private SearchCriteria? lastSearch;
    public ExcursionBookingWindow(ExcursionBookingService booking, TripService trips, UserSession session, IAppLogger logger)
    {
        this.booking = booking;
        this.trips = trips;
        this.session = session;
        this.logger = logger;
        InitializeComponent();
        ExcursionCityBox.Text = "Київ";
        ExcursionDatePicker.DisplayDateStart = DateTime.Today;
        ExcursionDatePicker.DisplayDateEnd = DateTime.Today.AddDays(59);
        ExcursionDatePicker.SelectedDate = DateTime.Today.AddDays(1);
        BackButton.Click += (_, _) => Close();
        SearchExcursionsButton.Click += async (_, _) => await ExecuteAsync(SearchAsync);
        ReserveExcursionButton.Click += async (_, _) => await ExecuteAsync(ReserveAsync);
        OffersGrid.SelectionChanged += OnExcursionChanged;
        ExcursionTripBox.SelectionChanged += OnTripChanged;
        Closing += (_, e) =>
        {
            if (busy)
            {
                e.Cancel = true;
            }
        };
        Loaded += async (_, _) => await ExecuteAsync(LoadAsync);
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
        ExcursionStatusText.Text = "Виконується…";
        try
        {
            await action();
            if (!validationFailed) { ExcursionStatusText.Text = "Готово."; }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "ExcursionBooking.Window", session.CurrentUser?.UserId);
            string message = exception is ArgumentException or UnauthorizedAccessException ? exception.Message : "Не вдалося виконати дію. Деталі збережено в журналі.";
            ExcursionStatusText.Text = message;
            MessageBox.Show(this, message, "Бронювання екскурсії", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
            busy = false;
        }
    }

    private async Task LoadAsync()
    {
        var check1 = session.TryRequireRole("Tourist");
        if (check1.IsFailure)
        {
            ShowValidationError(check1.Error, log: true);
            return;
        }

        GuestNameBox.Text = session.CurrentUser?.FirstName ?? string.Empty;
        var check2 = await trips.ListAsync();
        if (check2.IsFailure)
        {
            ShowValidationError(check2.Error, log: false);
            return;
        }

        IReadOnlyList<Trip> availableTrips = check2.Value!;
        ExcursionTripBox.ItemsSource = availableTrips.OrderByDescending(x => x.StartDate).ToList();
        Trip? selectedTrip = availableTrips.Where(x => x.EndDate.Date >= DateTime.Today).OrderBy(x => x.StartDate).FirstOrDefault();
        ExcursionTripBox.SelectedItem = selectedTrip;
        if (selectedTrip is not null)
        {
            ExcursionCityBox.Text = selectedTrip.City;
            ParticipantsBox.Text = Math.Clamp(selectedTrip.PeopleCount, 1, 1000).ToString(CultureInfo.CurrentCulture);
            DateTime date = selectedTrip.StartDate.Date < DateTime.Today ? DateTime.Today : selectedTrip.StartDate.Date;
            if (date <= DateTime.Today.AddDays(59))
            {
                ExcursionDatePicker.SelectedDate = date;
            }

            await RefreshReservationsAsync();
        }
    }

    private Result<SearchCriteria> ReadCriteria()
    {
        DateTime? date = ExcursionDatePicker.SelectedDate;
        if (date is null)
        {
            return new Failure("Оберіть дату екскурсії.");
        }

        if (!int.TryParse(ParticipantsBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int participants))
        {
            return new Failure("Кількість учасників має бути цілим числом.");
        }

        return new SearchCriteria(ExcursionCityBox.Text.Trim(), date.Value.Date, participants);
    }

    private async Task SearchAsync()
    {
        ExcursionConfirmationText.Text = string.Empty;
        var check3 = ReadCriteria();
        if (check3.IsFailure)
        {
            ShowValidationError(check3.Error, log: true);
            return;
        }

        await RefreshSearchAsync(check3.Value!);
    }

    private async Task RefreshSearchAsync(SearchCriteria criteria)
    {
        lastSearch = null;
        OffersGrid.ItemsSource = null;
        ClearExcursionDetails();
        var check4 = await booking.SearchAsync(criteria.City, criteria.Date, criteria.ParticipantsCount);
        if (check4.IsFailure)
        {
            ShowValidationError(check4.Error, log: false);
            return;
        }

        IReadOnlyList<ExcursionOffer> result = check4.Value!;
        lastSearch = criteria;
        OffersGrid.ItemsSource = result;
        SelectedExcursionText.Text = result.Count == 0 ? "Екскурсій із потрібною кількістю вільних місць немає." : "Виберіть екскурсію в таблиці.";
    }

    private void OnExcursionChanged(object sender, SelectionChangedEventArgs e)
    {
        ClearExcursionDetails();
        if (OffersGrid.SelectedItem is not ExcursionOffer offer || lastSearch is not SearchCriteria criteria)
        {
            return;
        }

        ExcursionSession excursion = offer.Session;
        decimal total = excursion.PricePerPerson * criteria.ParticipantsCount;
        SelectedExcursionText.Text = excursion.Title;
        ExcursionDetailsText.Text = $"{excursion.Description}\n\n" + $"Місто: {excursion.City}\n" + $"Місце зустрічі: {excursion.MeetingAddress}\n" + $"Початок: {excursion.StartsAt:dd.MM.yyyy HH:mm}\n" + $"Тривалість: {excursion.DurationMinutes} хв\n" + $"Вільних місць: {offer.AvailableSeats}";
        ExcursionSummaryText.Text = $"Учасників: {criteria.ParticipantsCount}\n" + $"{excursion.PricePerPerson:N2} ₴ " + $"× {criteria.ParticipantsCount} = {total:N2} ₴";
    }

    private void ClearExcursionDetails()
    {
        SelectedExcursionText.Text = "Виберіть екскурсію в таблиці.";
        ExcursionDetailsText.Text = string.Empty;
        ExcursionSummaryText.Text = "Тут з’явиться розрахунок вартості.";
    }

    private async void OnTripChanged(object sender, SelectionChangedEventArgs e)
    {
        if (busy)
        {
            return;
        }

        await ExecuteAsync(RefreshReservationsAsync);
    }

    private async Task RefreshReservationsAsync()
    {
        ExcursionReservationsGrid.ItemsSource = null;
        if (ExcursionTripBox.SelectedItem is not Trip trip)
        {
            return;
        }

        var check5 = await booking.ListAsync(trip.TripId);
        if (check5.IsFailure)
        {
            ShowValidationError(check5.Error, log: false);
            return;
        }

        ExcursionReservationsGrid.ItemsSource = check5.Value!;
    }

    private async Task ReserveAsync()
    {
        if (ExcursionTripBox.SelectedItem is not Trip trip)
        {
            ShowValidationError("Оберіть подорож. Якщо подорожей немає — спочатку створіть її.", log: true);
            return;
        }

        if (OffersGrid.SelectedItem is not ExcursionOffer offer)
        {
            ShowValidationError("Оберіть екскурсію в таблиці.", log: true);
            return;
        }

        var check6 = ReadCriteria();
        if (check6.IsFailure)
        {
            ShowValidationError(check6.Error, log: true);
            return;
        }

        SearchCriteria criteria = check6.Value!;
        if (lastSearch is not SearchCriteria previous || !string.Equals(criteria.City, previous.City, StringComparison.OrdinalIgnoreCase) || criteria.Date != previous.Date || criteria.ParticipantsCount != previous.ParticipantsCount)
        {
            ShowValidationError("Параметри пошуку змінилися. " + "Натисніть «Знайти екскурсії» " + "та виберіть пропозицію знову.", log: true);
            return;
        }

        ExcursionConfirmationText.Text = string.Empty;
        var check7 = await booking.ReserveAsync(trip.TripId, offer.Session.ExcursionSessionId, criteria.ParticipantsCount, GuestNameBox.Text);
        if (check7.IsFailure)
        {
            ShowValidationError(check7.Error, log: false);
            return;
        }

        ExcursionReservation reservation = check7.Value!;
        ExcursionConfirmationText.Text = $"Бронювання збережено!\n" + $"Код: {reservation.BookingCode}\n" + $"{offer.Session.Title}\n" + $"Початок: {offer.Session.StartsAt:dd.MM.yyyy HH:mm}\n" + $"Місце зустрічі: {offer.Session.MeetingAddress}\n" + $"Учасників: {reservation.ParticipantsCount}\n" + $"Відповідальний гість: {reservation.GuestName}\n" + $"Разом: {reservation.TotalPrice:N2} ₴\n" + "Навчальне бронювання без оплати.";
        await RefreshReservationsAsync();
        await RefreshSearchAsync(criteria);
    }

    private sealed record SearchCriteria(string City, DateTime Date, int ParticipantsCount);

    private void ShowValidationError(string message, bool log = false)
    {
        validationFailed = true;
        ExcursionStatusText.Text = message;
        if (log) logger.Information("ExcursionBookingWindow.ValidationFailed: " + message, session.CurrentUser?.UserId);
        MessageBox.Show(this, message, "Перевірте введені дані", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
