using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class TrainBookingWindow : Window
{
    private readonly TrainBookingService booking;
    private readonly TripService trips;
    private readonly UserSession session;
    private readonly IAppLogger logger;

    private bool busy;

    public TrainBookingWindow(
        TrainBookingService booking,
        TripService trips,
        UserSession session,
        IAppLogger logger)
    {
        this.booking = booking;
        this.trips = trips;
        this.session = session;
        this.logger = logger;

        InitializeComponent();

        DepartureCityBox.Text = "Львів";
        ArrivalCityBox.Text = "Київ";

        DepartureDatePicker.DisplayDateStart = DateTime.Today;
        DepartureDatePicker.DisplayDateEnd =
            DateTime.Today.AddDays(59);

        DepartureDatePicker.SelectedDate =
            DateTime.Today.AddDays(1);

        BackButton.Click += (_, _) => Close();

        SearchTrainsButton.Click += async (_, _) =>
            await ExecuteAsync(SearchAsync);

        ReserveTrainButton.Click += async (_, _) =>
            await ExecuteAsync(ReserveAsync);

        DeparturesGrid.SelectionChanged += OnDepartureChanged;
        WagonBox.SelectionChanged += OnWagonChanged;
        BookingTripBox.SelectionChanged += OnTripChanged;

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
        BookingStatusText.Text = "Виконується…";

        try
        {
            await action();

            BookingStatusText.Text = "Готово.";
        }
        catch (Exception exception)
        {
            logger.Error(
                exception,
                "TrainBooking.Window",
                session.CurrentUser?.UserId);

            string message = exception is ArgumentException
                or UnauthorizedAccessException
                ? exception.Message
                : "Не вдалося виконати дію. Деталі збережено в журналі.";

            BookingStatusText.Text = message;

            MessageBox.Show(
                this,
                message,
                "Бронювання поїзда",
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

        PassengerNameBox.Text =
            session.CurrentUser?.FirstName ?? string.Empty;

        IReadOnlyList<Trip> availableTrips =
            await trips.ListAsync();

        BookingTripBox.ItemsSource = availableTrips
            .OrderByDescending(x => x.StartDate)
            .ToList();

        BookingTripBox.SelectedItem = availableTrips
            .Where(x => x.EndDate.Date >= DateTime.Today)
            .OrderBy(x => x.StartDate)
            .FirstOrDefault();

        if (BookingTripBox.SelectedItem is Trip trip)
        {
            if (trip.StartDate.Date >= DateTime.Today
                && trip.StartDate.Date
                    <= DateTime.Today.AddDays(59))
            {
                DepartureDatePicker.SelectedDate =
                    trip.StartDate.Date;
            }

            await RefreshReservationsAsync();
        }
    }

    private async Task SearchAsync()
    {
        DateTime date = DepartureDatePicker.SelectedDate
            ?? throw new ArgumentException(
                "Оберіть дату відправлення.");

        ClearSeatSelection();
        BookingConfirmationText.Text = string.Empty;
        DeparturesGrid.ItemsSource = null;

        IReadOnlyList<TrainDeparture> result =
            await booking.SearchAsync(
                DepartureCityBox.Text,
                ArrivalCityBox.Text,
                date);

        DeparturesGrid.ItemsSource = result;

        SelectedTrainText.Text = result.Count == 0
            ? "За цим напрямком і датою рейсів немає."
            : "Виберіть рейс у таблиці.";
    }

    private async void OnDepartureChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (busy)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            ClearSeatSelection();

            if (DeparturesGrid.SelectedItem
                is not TrainDeparture departure)
            {
                SelectedTrainText.Text =
                    "Спочатку виберіть рейс у таблиці.";

                return;
            }

            SelectedTrainText.Text =
                $"Поїзд {departure.TrainNumber} · "
                + $"{departure.DepartureCity} → "
                + $"{departure.ArrivalCity} · "
                + $"{departure.DepartureAt:dd.MM.yyyy HH:mm} · "
                + $"{departure.TicketPrice:N2} ₴";

            WagonBox.ItemsSource = Enumerable
                .Range(1, departure.WagonCount)
                .ToList();

            WagonBox.SelectedIndex = 0;

            await RefreshSeatsAsync();
        });
    }

    private async void OnWagonChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (busy)
        {
            return;
        }

        await ExecuteAsync(RefreshSeatsAsync);
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

    private void ClearSeatSelection()
    {
        WagonBox.ItemsSource = null;
        SeatsList.ItemsSource = null;
    }

    private async Task RefreshSeatsAsync()
    {
        SeatsList.ItemsSource = null;

        if (DeparturesGrid.SelectedItem
                is not TrainDeparture departure
            || WagonBox.SelectedItem is not int wagon)
        {
            return;
        }

        IReadOnlyList<int> seats =
            await booking.AvailableSeatsAsync(
                departure.TrainDepartureId,
                wagon);

        SeatsList.ItemsSource = seats;
    }

    private async Task RefreshReservationsAsync()
    {
        ReservationsGrid.ItemsSource = null;

        if (BookingTripBox.SelectedItem is not Trip trip)
        {
            return;
        }

        ReservationsGrid.ItemsSource =
            await booking.ListAsync(trip.TripId);
    }

    private async Task ReserveAsync()
    {
        if (BookingTripBox.SelectedItem is not Trip trip)
        {
            throw new ArgumentException(
                "Оберіть подорож. Якщо подорожей немає — спочатку створіть її.");
        }

        if (DeparturesGrid.SelectedItem
            is not TrainDeparture departure)
        {
            throw new ArgumentException(
                "Оберіть рейс у таблиці.");
        }

        if (WagonBox.SelectedItem is not int wagon)
        {
            throw new ArgumentException(
                "Оберіть вагон.");
        }

        if (SeatsList.SelectedItem is not int seat)
        {
            throw new ArgumentException(
                "Натисніть на номер вільного місця.");
        }

        BookingConfirmationText.Text = string.Empty;

        TrainReservation reservation;

        try
        {
            reservation = await booking.ReserveAsync(
                trip.TripId,
                departure.TrainDepartureId,
                wagon,
                seat,
                PassengerNameBox.Text);
        }
        catch (ArgumentException)
        {
            await RefreshSeatsAsync();
            throw;
        }

        string price = reservation.TotalPrice.ToString(
            "N2",
            CultureInfo.CurrentCulture);

        BookingConfirmationText.Text =
            $"Бронювання збережено!\n"
            + $"Код: {reservation.BookingCode}\n"
            + $"Поїзд {departure.TrainNumber} · "
            + $"вагон {wagon} · місце {seat}\n"
            + $"Пасажир: {reservation.PassengerName}\n"
            + $"Вартість: {price} ₴\n"
            + "Це навчальне бронювання без оплати.";

        await RefreshSeatsAsync();
        await RefreshReservationsAsync();
    }
}