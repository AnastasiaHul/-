using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class CarBookingWindow : Window
{
    private readonly CarBookingService booking;
    private readonly TripService trips;
    private readonly UserSession session;
    private readonly IAppLogger logger;

    private bool busy;
    private SearchCriteria? lastSearch;

    public CarBookingWindow(
        CarBookingService booking,
        TripService trips,
        UserSession session,
        IAppLogger logger)
    {
        this.booking = booking;
        this.trips = trips;
        this.session = session;
        this.logger = logger;

        InitializeComponent();

        RentalCityBox.Text = "Київ";

        PickupDatePicker.DisplayDateStart = DateTime.Today;
        PickupDatePicker.DisplayDateEnd =
            DateTime.Today.AddDays(365);

        ReturnDatePicker.DisplayDateStart =
            DateTime.Today.AddDays(1);

        PickupDatePicker.SelectedDate =
            DateTime.Today.AddDays(1);

        ReturnDatePicker.SelectedDate =
            DateTime.Today.AddDays(3);

        BackButton.Click += (_, _) => Close();

        SearchCarsButton.Click += async (_, _) =>
            await ExecuteAsync(SearchAsync);

        ReserveCarButton.Click += async (_, _) =>
            await ExecuteAsync(ReserveAsync);

        CarsGrid.SelectionChanged += OnCarChanged;
        RentalTripBox.SelectionChanged += OnTripChanged;

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
        CarStatusText.Text = "Виконується…";

        try
        {
            await action();

            CarStatusText.Text = "Готово.";
        }
        catch (Exception exception)
        {
            logger.Error(
                exception,
                "CarBooking.Window",
                session.CurrentUser?.UserId);

            string message = exception is ArgumentException
                or UnauthorizedAccessException
                ? exception.Message
                : "Не вдалося виконати дію. Деталі збережено в журналі.";

            CarStatusText.Text = message;

            MessageBox.Show(
                this,
                message,
                "Оренда автомобіля",
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

        DriverNameBox.Text =
            session.CurrentUser?.FirstName ?? string.Empty;

        IReadOnlyList<Trip> availableTrips =
            await trips.ListAsync();

        RentalTripBox.ItemsSource = availableTrips
            .OrderByDescending(x => x.StartDate)
            .ToList();

        Trip? selectedTrip = availableTrips
            .Where(x => x.EndDate.Date > DateTime.Today)
            .OrderBy(x => x.StartDate)
            .FirstOrDefault();

        RentalTripBox.SelectedItem = selectedTrip;

        if (selectedTrip is not null)
        {
            RentalCityBox.Text = selectedTrip.City;

            RequiredSeatsBox.Text = Math.Clamp(
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
                PickupDatePicker.SelectedDate = start;
                ReturnDatePicker.SelectedDate = end;
            }

            await RefreshReservationsAsync();
        }
    }

    private SearchCriteria ReadCriteria()
    {
        DateTime pickupDate = PickupDatePicker.SelectedDate
            ?? throw new ArgumentException(
                "Оберіть дату отримання автомобіля.");

        DateTime returnDate = ReturnDatePicker.SelectedDate
            ?? throw new ArgumentException(
                "Оберіть дату повернення автомобіля.");

        int seats = ReadInteger(
            RequiredSeatsBox.Text,
            "Кількість місць має бути цілим числом.");

        return new SearchCriteria(
            RentalCityBox.Text.Trim(),
            pickupDate.Date,
            returnDate.Date,
            seats);
    }

    private static int ReadInteger(
        string value,
        string message)
    {
        if (!int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.CurrentCulture,
            out int result))
        {
            throw new ArgumentException(message);
        }

        return result;
    }

    private async Task SearchAsync()
    {
        CarConfirmationText.Text = string.Empty;

        await RefreshSearchAsync(ReadCriteria());
    }

    private async Task RefreshSearchAsync(
        SearchCriteria criteria)
    {
        lastSearch = null;
        CarsGrid.ItemsSource = null;
        ClearCarDetails();

        IReadOnlyList<RentalCar> result =
            await booking.SearchAsync(
                criteria.City,
                criteria.PickupDate,
                criteria.ReturnDate,
                criteria.RequiredSeats);

        lastSearch = criteria;
        CarsGrid.ItemsSource = result;

        SelectedCarText.Text = result.Count == 0
            ? "Вільних автомобілів за цими параметрами немає."
            : "Виберіть автомобіль у таблиці.";
    }

    private void OnCarChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        ClearCarDetails();

        if (CarsGrid.SelectedItem is not RentalCar car
            || lastSearch is not SearchCriteria criteria)
        {
            return;
        }

        int days =
            (criteria.ReturnDate - criteria.PickupDate).Days;

        decimal total = car.PricePerDay * days;

        SelectedCarText.Text = $"{car.Brand} {car.Model}";

        CarDetailsText.Text =
            $"Місто: {car.City}\n"
            + $"Отримання та повернення: {car.PickupAddress}\n"
            + $"Коробка передач: {car.Transmission}\n"
            + $"Кількість місць: {car.SeatsCount}";

        RentalSummaryText.Text =
            $"{criteria.PickupDate:dd.MM.yyyy} — "
            + $"{criteria.ReturnDate:dd.MM.yyyy}\n"
            + $"Днів оренди: {days}\n"
            + $"{car.PricePerDay:N2} ₴ "
            + $"× {days} = {total:N2} ₴";
    }

    private void ClearCarDetails()
    {
        SelectedCarText.Text =
            "Виберіть автомобіль у таблиці.";

        CarDetailsText.Text = string.Empty;

        RentalSummaryText.Text =
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
        CarReservationsGrid.ItemsSource = null;

        if (RentalTripBox.SelectedItem is not Trip trip)
        {
            return;
        }

        CarReservationsGrid.ItemsSource =
            await booking.ListAsync(trip.TripId);
    }

    private async Task ReserveAsync()
    {
        if (RentalTripBox.SelectedItem is not Trip trip)
        {
            throw new ArgumentException(
                "Оберіть подорож. Якщо подорожей немає — спочатку створіть її.");
        }

        if (CarsGrid.SelectedItem is not RentalCar car)
        {
            throw new ArgumentException(
                "Оберіть автомобіль у таблиці.");
        }

        SearchCriteria criteria = ReadCriteria();

        if (lastSearch is not SearchCriteria previous
            || !string.Equals(
                criteria.City,
                previous.City,
                StringComparison.OrdinalIgnoreCase)
            || criteria.PickupDate != previous.PickupDate
            || criteria.ReturnDate != previous.ReturnDate
            || criteria.RequiredSeats != previous.RequiredSeats)
        {
            throw new ArgumentException(
                "Параметри пошуку змінилися. "
                + "Натисніть «Знайти авто» та виберіть автомобіль знову.");
        }

        int driverAge = ReadInteger(
            DriverAgeBox.Text,
            "Введіть вік водія цілим числом.");

        CarConfirmationText.Text = string.Empty;

        CarReservation reservation =
            await booking.ReserveAsync(
                trip.TripId,
                car.RentalCarId,
                criteria.PickupDate,
                criteria.ReturnDate,
                criteria.RequiredSeats,
                DriverNameBox.Text,
                driverAge);

        CarConfirmationText.Text =
            $"Бронювання збережено!\n"
            + $"Код: {reservation.BookingCode}\n"
            + $"{car.Brand} {car.Model}\n"
            + $"Водій: {reservation.DriverName}\n"
            + $"Отримання: {reservation.PickupDate:dd.MM.yyyy}\n"
            + $"Повернення: {reservation.ReturnDate:dd.MM.yyyy}\n"
            + $"Адреса: {car.PickupAddress}\n"
            + $"Разом: {reservation.TotalPrice:N2} ₴\n"
            + "Навчальна оренда без оплати.";

        await RefreshReservationsAsync();
        await RefreshSearchAsync(criteria);
    }

    private sealed record SearchCriteria(
        string City,
        DateTime PickupDate,
        DateTime ReturnDate,
        int RequiredSeats);
}