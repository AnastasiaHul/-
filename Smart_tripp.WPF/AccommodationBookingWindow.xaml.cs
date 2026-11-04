using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace Smart_tripp.WPF;
public partial class AccommodationBookingWindow : Window
{
    private readonly AccommodationBookingService booking;
    private readonly TripService trips;
    private readonly UserSession session;
    private readonly IAppLogger logger;
    private bool busy;
    private bool validationFailed;
    private SearchCriteria? lastSearch;
    public AccommodationBookingWindow(AccommodationBookingService booking, TripService trips, UserSession session, IAppLogger logger)
    {
        this.booking = booking;
        this.trips = trips;
        this.session = session;
        this.logger = logger;
        InitializeComponent();
        HousingCityBox.Text = "Київ";
        CheckInPicker.DisplayDateStart = DateTime.Today;
        CheckInPicker.DisplayDateEnd = DateTime.Today.AddDays(365);
        CheckOutPicker.DisplayDateStart = DateTime.Today.AddDays(1);
        CheckInPicker.SelectedDate = DateTime.Today.AddDays(1);
        CheckOutPicker.SelectedDate = DateTime.Today.AddDays(3);
        BackButton.Click += (_, _) => Close();
        SearchHousingButton.Click += async (_, _) => await ExecuteAsync(SearchAsync);
        ReserveHousingButton.Click += async (_, _) => await ExecuteAsync(ReserveAsync);
        AccommodationsGrid.SelectionChanged += OnHousingChanged;
        HousingTripBox.SelectionChanged += OnTripChanged;
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
        HousingStatusText.Text = "Виконується…";
        try
        {
            await action();
            if (!validationFailed) { HousingStatusText.Text = "Готово."; }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "AccommodationBooking.Window", session.CurrentUser?.UserId);
            string message = exception is ArgumentException or UnauthorizedAccessException ? exception.Message : "Не вдалося виконати дію. Деталі збережено в журналі.";
            HousingStatusText.Text = message;
            MessageBox.Show(this, message, "Бронювання житла", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        HousingTripBox.ItemsSource = availableTrips.OrderByDescending(x => x.StartDate).ToList();
        Trip? selectedTrip = availableTrips.Where(x => x.EndDate.Date > DateTime.Today).OrderBy(x => x.StartDate).FirstOrDefault();
        HousingTripBox.SelectedItem = selectedTrip;
        if (selectedTrip is not null)
        {
            HousingCityBox.Text = selectedTrip.City;
            GuestsCountBox.Text = Math.Clamp(selectedTrip.PeopleCount, 1, 20).ToString(CultureInfo.CurrentCulture);
            DateTime start = selectedTrip.StartDate.Date < DateTime.Today ? DateTime.Today : selectedTrip.StartDate.Date;
            DateTime end = selectedTrip.EndDate.Date;
            if ((end - start).Days > 30)
            {
                end = start.AddDays(30);
            }

            if (start <= DateTime.Today.AddDays(365) && end > start)
            {
                CheckInPicker.SelectedDate = start;
                CheckOutPicker.SelectedDate = end;
            }

            await RefreshReservationsAsync();
        }
    }

    private Result<SearchCriteria> ReadCriteria()
    {
        DateTime? checkIn = CheckInPicker.SelectedDate;
        if (checkIn is null)
        {
            return new Failure("Оберіть дату заїзду.");
        }

        DateTime? checkOut = CheckOutPicker.SelectedDate;
        if (checkOut is null)
        {
            return new Failure("Оберіть дату виїзду.");
        }

        if (!int.TryParse(GuestsCountBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int guests))
        {
            return new Failure("Кількість гостей має бути цілим числом.");
        }

        return new SearchCriteria(HousingCityBox.Text.Trim(), checkIn.Value.Date, checkOut.Value.Date, guests);
    }

    private async Task SearchAsync()
    {
        HousingConfirmationText.Text = string.Empty;
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
        AccommodationsGrid.ItemsSource = null;
        ClearHousingDetails();
        var check4 = await booking.SearchAsync(criteria.City, criteria.CheckIn, criteria.CheckOut, criteria.GuestsCount);
        if (check4.IsFailure)
        {
            ShowValidationError(check4.Error, log: false);
            return;
        }

        IReadOnlyList<Accommodation> result = check4.Value!;
        lastSearch = criteria;
        AccommodationsGrid.ItemsSource = result;
        SelectedHousingText.Text = result.Count == 0 ? "Вільного житла за цими параметрами немає." : "Виберіть пропозицію в таблиці.";
    }

    private void OnHousingChanged(object sender, SelectionChangedEventArgs e)
    {
        ClearHousingDetails();
        if (AccommodationsGrid.SelectedItem is not Accommodation accommodation || lastSearch is not SearchCriteria criteria)
        {
            return;
        }

        int nights = (criteria.CheckOut - criteria.CheckIn).Days;
        decimal total = accommodation.PricePerNight * nights;
        SelectedHousingText.Text = $"{accommodation.Name}\n{accommodation.Address}";
        HousingDescriptionText.Text = accommodation.Description;
        StaySummaryText.Text = $"{criteria.CheckIn:dd.MM.yyyy} — " + $"{criteria.CheckOut:dd.MM.yyyy}\n" + $"Ночей: {nights} · " + $"Гостей: {criteria.GuestsCount}\n" + $"{accommodation.PricePerNight:N2} ₴ " + $"× {nights} = {total:N2} ₴";
    }

    private void ClearHousingDetails()
    {
        SelectedHousingText.Text = "Виберіть пропозицію в таблиці.";
        HousingDescriptionText.Text = string.Empty;
        StaySummaryText.Text = "Тут з’явиться розрахунок вартості.";
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
        HousingReservationsGrid.ItemsSource = null;
        if (HousingTripBox.SelectedItem is not Trip trip)
        {
            return;
        }

        var check5 = await booking.ListAsync(trip.TripId);
        if (check5.IsFailure)
        {
            ShowValidationError(check5.Error, log: false);
            return;
        }

        HousingReservationsGrid.ItemsSource = check5.Value!;
    }

    private async Task ReserveAsync()
    {
        if (HousingTripBox.SelectedItem is not Trip trip)
        {
            ShowValidationError("Оберіть подорож. Якщо подорожей немає — спочатку створіть її.", log: true);
            return;
        }

        if (AccommodationsGrid.SelectedItem is not Accommodation accommodation)
        {
            ShowValidationError("Оберіть житло в таблиці.", log: true);
            return;
        }

        var check6 = ReadCriteria();
        if (check6.IsFailure)
        {
            ShowValidationError(check6.Error, log: true);
            return;
        }

        SearchCriteria criteria = check6.Value!;
        if (lastSearch is not SearchCriteria previous || !string.Equals(criteria.City, previous.City, StringComparison.OrdinalIgnoreCase) || criteria.CheckIn != previous.CheckIn || criteria.CheckOut != previous.CheckOut || criteria.GuestsCount != previous.GuestsCount)
        {
            ShowValidationError("Параметри пошуку змінилися. " + "Натисніть «Знайти житло» та виберіть пропозицію знову.", log: true);
            return;
        }

        HousingConfirmationText.Text = string.Empty;
        var check7 = await booking.ReserveAsync(trip.TripId, accommodation.AccommodationId, criteria.CheckIn, criteria.CheckOut, criteria.GuestsCount, GuestNameBox.Text);
        if (check7.IsFailure)
        {
            ShowValidationError(check7.Error, log: false);
            return;
        }

        AccommodationReservation reservation = check7.Value!;
        HousingConfirmationText.Text = $"Бронювання збережено!\n" + $"Код: {reservation.BookingCode}\n" + $"{accommodation.Name}\n" + $"Заїзд: {reservation.CheckIn:dd.MM.yyyy}\n" + $"Виїзд: {reservation.CheckOut:dd.MM.yyyy}\n" + $"Гостей: {reservation.GuestsCount}\n" + $"Разом: {reservation.TotalPrice:N2} ₴\n" + "Навчальне бронювання без оплати.";
        await RefreshReservationsAsync();
        await RefreshSearchAsync(criteria);
    }

    private sealed record SearchCriteria(string City, DateTime CheckIn, DateTime CheckOut, int GuestsCount);

    private void ShowValidationError(string message, bool log = false)
    {
        validationFailed = true;
        HousingStatusText.Text = message;
        if (log) logger.Information("AccommodationBookingWindow.ValidationFailed: " + message, session.CurrentUser?.UserId);
        MessageBox.Show(this, message, "Перевірте введені дані", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
