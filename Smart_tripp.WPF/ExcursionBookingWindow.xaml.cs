using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class ExcursionBookingWindow : Window
{
    private readonly ExcursionBookingService booking;
    private readonly TripService trips;
    private readonly UserSession session;
    private readonly IAppLogger logger;

    private bool busy;
    private SearchCriteria? lastSearch;

    public ExcursionBookingWindow(
        ExcursionBookingService booking,
        TripService trips,
        UserSession session,
        IAppLogger logger)
    {
        this.booking = booking;
        this.trips = trips;
        this.session = session;
        this.logger = logger;

        InitializeComponent();

        ExcursionCityBox.Text = "Київ";

        ExcursionDatePicker.DisplayDateStart = DateTime.Today;
        ExcursionDatePicker.DisplayDateEnd =
            DateTime.Today.AddDays(59);

        ExcursionDatePicker.SelectedDate =
            DateTime.Today.AddDays(1);

        BackButton.Click += (_, _) => Close();

        SearchExcursionsButton.Click += async (_, _) =>
            await ExecuteAsync(SearchAsync);

        ReserveExcursionButton.Click += async (_, _) =>
            await ExecuteAsync(ReserveAsync);

        OffersGrid.SelectionChanged += OnExcursionChanged;
        ExcursionTripBox.SelectionChanged += OnTripChanged;

        Closing += (_, e) =>
        {
            if (busy)
            {
                e.Cancel = true;
            }
        };

        Loaded += async (_, _) =>
            await ExecuteAsync(LoadAsync);
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        IsEnabled = false;
        ExcursionStatusText.Text = "Виконується…";

        try
        {
            await action();

            ExcursionStatusText.Text = "Готово.";
        }
        catch (Exception exception)
        {
            logger.Error(
                exception,
                "ExcursionBooking.Window",
                session.CurrentUser?.UserId);

            string message = exception is ArgumentException
                or UnauthorizedAccessException
                ? exception.Message
                : "Не вдалося виконати дію. Деталі збережено в журналі.";

            ExcursionStatusText.Text = message;

            MessageBox.Show(
                this,
                message,
                "Бронювання екскурсії",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
            busy = false;
        }
    }

    private async Task LoadAsync()
    {
        session.RequireRole("Tourist");

        GuestNameBox.Text =
            session.CurrentUser?.FirstName ?? string.Empty;

        IReadOnlyList<Trip> availableTrips =
            await trips.ListAsync();

        ExcursionTripBox.ItemsSource = availableTrips
            .OrderByDescending(x => x.StartDate)
            .ToList();

        Trip? selectedTrip = availableTrips
            .Where(x => x.EndDate.Date >= DateTime.Today)
            .OrderBy(x => x.StartDate)
            .FirstOrDefault();

        ExcursionTripBox.SelectedItem = selectedTrip;

        if (selectedTrip is not null)
        {
            ExcursionCityBox.Text = selectedTrip.City;

            ParticipantsBox.Text = Math.Clamp(
                selectedTrip.PeopleCount,
                1,
                1000).ToString(CultureInfo.CurrentCulture);

            DateTime date = selectedTrip.StartDate.Date
                < DateTime.Today
                ? DateTime.Today
                : selectedTrip.StartDate.Date;

            if (date <= DateTime.Today.AddDays(59))
            {
                ExcursionDatePicker.SelectedDate = date;
            }

            await RefreshReservationsAsync();
        }
    }

    private SearchCriteria ReadCriteria()
    {
        DateTime date = ExcursionDatePicker.SelectedDate
            ?? throw new ArgumentException(
                "Оберіть дату екскурсії.");

        if (!int.TryParse(
            ParticipantsBox.Text,
            NumberStyles.Integer,
            CultureInfo.CurrentCulture,
            out int participants))
        {
            throw new ArgumentException(
                "Кількість учасників має бути цілим числом.");
        }

        return new SearchCriteria(
            ExcursionCityBox.Text.Trim(),
            date.Date,
            participants);
    }

    private async Task SearchAsync()
    {
        ExcursionConfirmationText.Text = string.Empty;

        await RefreshSearchAsync(ReadCriteria());
    }

    private async Task RefreshSearchAsync(
        SearchCriteria criteria)
    {
        lastSearch = null;
        OffersGrid.ItemsSource = null;
        ClearExcursionDetails();

        IReadOnlyList<ExcursionOffer> result =
            await booking.SearchAsync(
                criteria.City,
                criteria.Date,
                criteria.ParticipantsCount);

        lastSearch = criteria;
        OffersGrid.ItemsSource = result;

        SelectedExcursionText.Text = result.Count == 0
            ? "Екскурсій із потрібною кількістю вільних місць немає."
            : "Виберіть екскурсію в таблиці.";
    }

    private void OnExcursionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        ClearExcursionDetails();

        if (OffersGrid.SelectedItem is not ExcursionOffer offer
            || lastSearch is not SearchCriteria criteria)
        {
            return;
        }

        ExcursionSession excursion = offer.Session;

        decimal total =
            excursion.PricePerPerson * criteria.ParticipantsCount;

        SelectedExcursionText.Text = excursion.Title;

        ExcursionDetailsText.Text =
            $"{excursion.Description}\n\n"
            + $"Місто: {excursion.City}\n"
            + $"Місце зустрічі: {excursion.MeetingAddress}\n"
            + $"Початок: {excursion.StartsAt:dd.MM.yyyy HH:mm}\n"
            + $"Тривалість: {excursion.DurationMinutes} хв\n"
            + $"Вільних місць: {offer.AvailableSeats}";

        ExcursionSummaryText.Text =
            $"Учасників: {criteria.ParticipantsCount}\n"
            + $"{excursion.PricePerPerson:N2} ₴ "
            + $"× {criteria.ParticipantsCount} = {total:N2} ₴";
    }

    private void ClearExcursionDetails()
    {
        SelectedExcursionText.Text =
            "Виберіть екскурсію в таблиці.";

        ExcursionDetailsText.Text = string.Empty;

        ExcursionSummaryText.Text =
            "Тут з’явиться розрахунок вартості.";
    }

    private async void OnTripChanged(
        object sender,
        SelectionChangedEventArgs e)
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

        ExcursionReservationsGrid.ItemsSource =
            await booking.ListAsync(trip.TripId);
    }

    private async Task ReserveAsync()
    {
        if (ExcursionTripBox.SelectedItem is not Trip trip)
        {
            throw new ArgumentException(
                "Оберіть подорож. Якщо подорожей немає — спочатку створіть її.");
        }

        if (OffersGrid.SelectedItem is not ExcursionOffer offer)
        {
            throw new ArgumentException(
                "Оберіть екскурсію в таблиці.");
        }

        SearchCriteria criteria = ReadCriteria();

        if (lastSearch is not SearchCriteria previous
            || !string.Equals(
                criteria.City,
                previous.City,
                StringComparison.OrdinalIgnoreCase)
            || criteria.Date != previous.Date
            || criteria.ParticipantsCount
                != previous.ParticipantsCount)
        {
            throw new ArgumentException(
                "Параметри пошуку змінилися. "
                + "Натисніть «Знайти екскурсії» "
                + "та виберіть пропозицію знову.");
        }

        ExcursionConfirmationText.Text = string.Empty;

        ExcursionReservation reservation =
            await booking.ReserveAsync(
                trip.TripId,
                offer.Session.ExcursionSessionId,
                criteria.ParticipantsCount,
                GuestNameBox.Text);

        ExcursionConfirmationText.Text =
            $"Бронювання збережено!\n"
            + $"Код: {reservation.BookingCode}\n"
            + $"{offer.Session.Title}\n"
            + $"Початок: {offer.Session.StartsAt:dd.MM.yyyy HH:mm}\n"
            + $"Місце зустрічі: {offer.Session.MeetingAddress}\n"
            + $"Учасників: {reservation.ParticipantsCount}\n"
            + $"Відповідальний гість: {reservation.GuestName}\n"
            + $"Разом: {reservation.TotalPrice:N2} ₴\n"
            + "Навчальне бронювання без оплати.";

        await RefreshReservationsAsync();
        await RefreshSearchAsync(criteria);
    }

    private sealed record SearchCriteria(
        string City,
        DateTime Date,
        int ParticipantsCount);
}