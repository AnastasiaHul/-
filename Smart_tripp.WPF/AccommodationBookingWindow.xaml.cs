using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class AccommodationBookingWindow : Window
{
    private readonly AccommodationBookingService booking;
    private readonly TripService trips;
    private readonly UserSession session;
    private readonly IAppLogger logger;

    private bool busy;
    private SearchCriteria? lastSearch;

    public AccommodationBookingWindow(
        AccommodationBookingService booking,
        TripService trips,
        UserSession session,
        IAppLogger logger)
    {
        this.booking = booking;
        this.trips = trips;
        this.session = session;
        this.logger = logger;

        InitializeComponent();

        HousingCityBox.Text = "Київ";

        CheckInPicker.DisplayDateStart = DateTime.Today;
        CheckInPicker.DisplayDateEnd =
            DateTime.Today.AddDays(365);

        CheckOutPicker.DisplayDateStart =
            DateTime.Today.AddDays(1);

        CheckInPicker.SelectedDate =
            DateTime.Today.AddDays(1);

        CheckOutPicker.SelectedDate =
            DateTime.Today.AddDays(3);

        BackButton.Click += (_, _) => Close();

        SearchHousingButton.Click += async (_, _) =>
            await ExecuteAsync(SearchAsync);

        ReserveHousingButton.Click += async (_, _) =>
            await ExecuteAsync(ReserveAsync);

        AccommodationsGrid.SelectionChanged += OnHousingChanged;
        HousingTripBox.SelectionChanged += OnTripChanged;

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
        HousingStatusText.Text = "Виконується…";

        try
        {
            await action();

            HousingStatusText.Text = "Готово.";
        }
        catch (Exception exception)
        {
            logger.Error(
                exception,
                "AccommodationBooking.Window",
                session.CurrentUser?.UserId);

            string message = exception is ArgumentException
                or UnauthorizedAccessException
                ? exception.Message
                : "Не вдалося виконати дію. Деталі збережено в журналі.";

            HousingStatusText.Text = message;

            MessageBox.Show(
                this,
                message,
                "Бронювання житла",
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

        HousingTripBox.ItemsSource = availableTrips
            .OrderByDescending(x => x.StartDate)
            .ToList();

        Trip? selectedTrip = availableTrips
            .Where(x => x.EndDate.Date > DateTime.Today)
            .OrderBy(x => x.StartDate)
            .FirstOrDefault();

        HousingTripBox.SelectedItem = selectedTrip;

        if (selectedTrip is not null)
        {
            HousingCityBox.Text = selectedTrip.City;

            GuestsCountBox.Text = Math.Clamp(
                selectedTrip.PeopleCount,
                1,
                20).ToString(CultureInfo.CurrentCulture);

            DateTime start = selectedTrip.StartDate.Date
                < DateTime.Today
                ? DateTime.Today
                : selectedTrip.StartDate.Date;

            DateTime end = selectedTrip.EndDate.Date;

            if ((end - start).Days > 30)
            {
                end = start.AddDays(30);
            }

            if (start <= DateTime.Today.AddDays(365)
                && end > start)
            {
                CheckInPicker.SelectedDate = start;
                CheckOutPicker.SelectedDate = end;
            }

            await RefreshReservationsAsync();
        }
    }

    private SearchCriteria ReadCriteria()
    {
        DateTime checkIn = CheckInPicker.SelectedDate
            ?? throw new ArgumentException(
                "Оберіть дату заїзду.");

        DateTime checkOut = CheckOutPicker.SelectedDate
            ?? throw new ArgumentException(
                "Оберіть дату виїзду.");

        if (!int.TryParse(
            GuestsCountBox.Text,
            NumberStyles.Integer,
            CultureInfo.CurrentCulture,
            out int guests))
        {
            throw new ArgumentException(
                "Кількість гостей має бути цілим числом.");
        }

        return new SearchCriteria(
            HousingCityBox.Text.Trim(),
            checkIn.Date,
            checkOut.Date,
            guests);
    }

    private async Task SearchAsync()
    {
        HousingConfirmationText.Text = string.Empty;

        await RefreshSearchAsync(ReadCriteria());
    }

    private async Task RefreshSearchAsync(
        SearchCriteria criteria)
    {
        lastSearch = null;
        AccommodationsGrid.ItemsSource = null;
        ClearHousingDetails();

        IReadOnlyList<Accommodation> result =
            await booking.SearchAsync(
                criteria.City,
                criteria.CheckIn,
                criteria.CheckOut,
                criteria.GuestsCount);

        lastSearch = criteria;
        AccommodationsGrid.ItemsSource = result;

        SelectedHousingText.Text = result.Count == 0
            ? "Вільного житла за цими параметрами немає."
            : "Виберіть пропозицію в таблиці.";
    }

    private void OnHousingChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        ClearHousingDetails();

        if (AccommodationsGrid.SelectedItem
                is not Accommodation accommodation
            || lastSearch is not SearchCriteria criteria)
        {
            return;
        }

        int nights =
            (criteria.CheckOut - criteria.CheckIn).Days;

        decimal total =
            accommodation.PricePerNight * nights;

        SelectedHousingText.Text =
            $"{accommodation.Name}\n{accommodation.Address}";

        HousingDescriptionText.Text =
            accommodation.Description;

        StaySummaryText.Text =
            $"{criteria.CheckIn:dd.MM.yyyy} — "
            + $"{criteria.CheckOut:dd.MM.yyyy}\n"
            + $"Ночей: {nights} · "
            + $"Гостей: {criteria.GuestsCount}\n"
            + $"{accommodation.PricePerNight:N2} ₴ "
            + $"× {nights} = {total:N2} ₴";
    }

    private void ClearHousingDetails()
    {
        SelectedHousingText.Text =
            "Виберіть пропозицію в таблиці.";

        HousingDescriptionText.Text = string.Empty;

        StaySummaryText.Text =
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
        HousingReservationsGrid.ItemsSource = null;

        if (HousingTripBox.SelectedItem is not Trip trip)
        {
            return;
        }

        HousingReservationsGrid.ItemsSource =
            await booking.ListAsync(trip.TripId);
    }

    private async Task ReserveAsync()
    {
        if (HousingTripBox.SelectedItem is not Trip trip)
        {
            throw new ArgumentException(
                "Оберіть подорож. Якщо подорожей немає — спочатку створіть її.");
        }

        if (AccommodationsGrid.SelectedItem
            is not Accommodation accommodation)
        {
            throw new ArgumentException(
                "Оберіть житло в таблиці.");
        }

        SearchCriteria criteria = ReadCriteria();

        if (lastSearch is not SearchCriteria previous
            || !string.Equals(
                criteria.City,
                previous.City,
                StringComparison.OrdinalIgnoreCase)
            || criteria.CheckIn != previous.CheckIn
            || criteria.CheckOut != previous.CheckOut
            || criteria.GuestsCount != previous.GuestsCount)
        {
            throw new ArgumentException(
                "Параметри пошуку змінилися. "
                + "Натисніть «Знайти житло» та виберіть пропозицію знову.");
        }

        HousingConfirmationText.Text = string.Empty;

        AccommodationReservation reservation =
            await booking.ReserveAsync(
                trip.TripId,
                accommodation.AccommodationId,
                criteria.CheckIn,
                criteria.CheckOut,
                criteria.GuestsCount,
                GuestNameBox.Text);

        HousingConfirmationText.Text =
            $"Бронювання збережено!\n"
            + $"Код: {reservation.BookingCode}\n"
            + $"{accommodation.Name}\n"
            + $"Заїзд: {reservation.CheckIn:dd.MM.yyyy}\n"
            + $"Виїзд: {reservation.CheckOut:dd.MM.yyyy}\n"
            + $"Гостей: {reservation.GuestsCount}\n"
            + $"Разом: {reservation.TotalPrice:N2} ₴\n"
            + "Навчальне бронювання без оплати.";

        await RefreshReservationsAsync();
        await RefreshSearchAsync(criteria);
    }

    private sealed record SearchCriteria(
        string City,
        DateTime CheckIn,
        DateTime CheckOut,
        int GuestsCount);
}