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
    private bool validationFailed;
    public TrainBookingWindow(TrainBookingService booking, TripService trips, UserSession session, IAppLogger logger)
    {
        this.booking = booking;
        this.trips = trips;
        this.session = session;
        this.logger = logger;
        InitializeComponent();
        DepartureCityBox.Text = "Львів";
        ArrivalCityBox.Text = "Київ";
        DepartureDatePicker.DisplayDateStart = DateTime.Today;
        DepartureDatePicker.DisplayDateEnd = DateTime.Today.AddDays(59);
        DepartureDatePicker.SelectedDate = DateTime.Today.AddDays(1);
        BackButton.Click += (_, _) => Close();
        SearchTrainsButton.Click += async (_, _) => await ExecuteAsync(SearchAsync);
        ReserveTrainButton.Click += async (_, _) => await ExecuteAsync(ReserveAsync);
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
        BookingStatusText.Text = "Виконується…";
        try
        {
            await action();
            if (!validationFailed) { BookingStatusText.Text = "Готово."; }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "TrainBooking.Window", session.CurrentUser?.UserId);
            string message = exception is ArgumentException or UnauthorizedAccessException ? exception.Message : "Не вдалося виконати дію. Деталі збережено в журналі.";
            BookingStatusText.Text = message;
            MessageBox.Show(this, message, "Бронювання поїзда", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        PassengerNameBox.Text = session.CurrentUser?.FirstName ?? string.Empty;
        var check2 = await trips.ListAsync();
        if (check2.IsFailure)
        {
            ShowValidationError(check2.Error, log: false);
            return;
        }

        IReadOnlyList<Trip> availableTrips = check2.Value!;
        BookingTripBox.ItemsSource = availableTrips.OrderByDescending(x => x.StartDate).ToList();
        BookingTripBox.SelectedItem = availableTrips.Where(x => x.EndDate.Date >= DateTime.Today).OrderBy(x => x.StartDate).FirstOrDefault();
        if (BookingTripBox.SelectedItem is Trip trip)
        {
            if (trip.StartDate.Date >= DateTime.Today && trip.StartDate.Date <= DateTime.Today.AddDays(59))
            {
                DepartureDatePicker.SelectedDate = trip.StartDate.Date;
            }

            await RefreshReservationsAsync();
        }
    }

    private async Task SearchAsync()
    {
        DateTime? date = DepartureDatePicker.SelectedDate;
        if (date is null)
        {
            ShowValidationError("Оберіть дату відправлення.", log: true);
            return;
        }

        ClearSeatSelection();
        BookingConfirmationText.Text = string.Empty;
        DeparturesGrid.ItemsSource = null;
        var check3 = await booking.SearchAsync(DepartureCityBox.Text, ArrivalCityBox.Text, date.Value);
        if (check3.IsFailure)
        {
            ShowValidationError(check3.Error, log: false);
            return;
        }

        IReadOnlyList<TrainDeparture> result = check3.Value!;
        DeparturesGrid.ItemsSource = result;
        SelectedTrainText.Text = result.Count == 0 ? "За цим напрямком і датою рейсів немає." : "Виберіть рейс у таблиці.";
    }

    private async void OnDepartureChanged(object sender, SelectionChangedEventArgs e)
    {
        if (busy)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            ClearSeatSelection();
            if (DeparturesGrid.SelectedItem is not TrainDeparture departure)
            {
                SelectedTrainText.Text = "Спочатку виберіть рейс у таблиці.";
                return;
            }

            SelectedTrainText.Text = $"Поїзд {departure.TrainNumber} · " + $"{departure.DepartureCity} → " + $"{departure.ArrivalCity} · " + $"{departure.DepartureAt:dd.MM.yyyy HH:mm} · " + $"{departure.TicketPrice:N2} ₴";
            WagonBox.ItemsSource = Enumerable.Range(1, departure.WagonCount).ToList();
            WagonBox.SelectedIndex = 0;
            await RefreshSeatsAsync();
        });
    }

    private async void OnWagonChanged(object sender, SelectionChangedEventArgs e)
    {
        if (busy)
        {
            return;
        }

        await ExecuteAsync(RefreshSeatsAsync);
    }

    private async void OnTripChanged(object sender, SelectionChangedEventArgs e)
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
        if (DeparturesGrid.SelectedItem is not TrainDeparture departure || WagonBox.SelectedItem is not int wagon)
        {
            return;
        }

        var check4 = await booking.AvailableSeatsAsync(departure.TrainDepartureId, wagon);
        if (check4.IsFailure)
        {
            ShowValidationError(check4.Error, log: false);
            return;
        }

        IReadOnlyList<int> seats = check4.Value!;
        SeatsList.ItemsSource = seats;
    }

    private async Task RefreshReservationsAsync()
    {
        ReservationsGrid.ItemsSource = null;
        if (BookingTripBox.SelectedItem is not Trip trip)
        {
            return;
        }

        var check5 = await booking.ListAsync(trip.TripId);
        if (check5.IsFailure)
        {
            ShowValidationError(check5.Error, log: false);
            return;
        }

        ReservationsGrid.ItemsSource = check5.Value!;
    }

    private async Task ReserveAsync()
    {
        if (BookingTripBox.SelectedItem is not Trip trip)
        {
            ShowValidationError("Оберіть подорож. Якщо подорожей немає — спочатку створіть її.", log: true);
            return;
        }

        if (DeparturesGrid.SelectedItem is not TrainDeparture departure)
        {
            ShowValidationError("Оберіть рейс у таблиці.", log: true);
            return;
        }

        if (WagonBox.SelectedItem is not int wagon)
        {
            ShowValidationError("Оберіть вагон.", log: true);
            return;
        }

        if (SeatsList.SelectedItem is not int seat)
        {
            ShowValidationError("Натисніть на номер вільного місця.", log: true);
            return;
        }

        BookingConfirmationText.Text = string.Empty;
        var reservationResult = await booking.ReserveAsync(
            trip.TripId,
            departure.TrainDepartureId,
            wagon,
            seat,
            PassengerNameBox.Text);

        if (reservationResult.IsFailure)
        {
            await RefreshSeatsAsync();
            ShowValidationError(reservationResult.Error);
            return;
        }

        TrainReservation reservation = reservationResult.Value!;

        string price = reservation.TotalPrice.ToString("N2", CultureInfo.CurrentCulture);
        BookingConfirmationText.Text = $"Бронювання збережено!\n" + $"Код: {reservation.BookingCode}\n" + $"Поїзд {departure.TrainNumber} · " + $"вагон {wagon} · місце {seat}\n" + $"Пасажир: {reservation.PassengerName}\n" + $"Вартість: {price} ₴\n" + "Це навчальне бронювання без оплати.";
        await RefreshSeatsAsync();
        await RefreshReservationsAsync();
    }

    private void ShowValidationError(string message, bool log = false)
    {
        validationFailed = true;
        BookingStatusText.Text = message;
        if (log) logger.Information("TrainBookingWindow.ValidationFailed: " + message, session.CurrentUser?.UserId);
        MessageBox.Show(this, message, "Перевірте введені дані", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
