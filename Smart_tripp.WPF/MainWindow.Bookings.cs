using System.Windows;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;

namespace Smart_tripp.WPF;

public partial class MainWindow
{
    private readonly TrainBookingService trainBooking;
    private readonly AccommodationBookingService housingBooking;
    private readonly CarBookingService carBooking;
    private readonly ExcursionBookingService excursionBooking;

    public MainWindow(
        AuthService auth,
        TripService trips,
        PlaceService places,
        CatalogService catalog,
        BudgetService budget,
        PackingService packing,
        UserSession session,
        IAppLogger logger,
        ItineraryService itinerary,
        MapRouteService mapRoutes,
        TrainBookingService trainBooking,
        AccommodationBookingService housingBooking,
        CarBookingService carBooking,
        ExcursionBookingService excursionBooking)
    {
        this.auth = auth;
        this.trips = trips;
        this.places = places;
        this.catalog = catalog;
        this.budget = budget;
        this.packing = packing;
        this.session = session;
        this.logger = logger;
        this.itinerary = itinerary;
        this.mapRoutes = mapRoutes;
        this.trainBooking = trainBooking;
        this.housingBooking = housingBooking;
        this.carBooking = carBooking;
        this.excursionBooking = excursionBooking;

        InitializeComponent();

        ClearTripEditor();
        ClearPlaceEditor();
        UpdateAccountNavigation();

        Loaded += async (_, _) => await ExecuteAsync(SearchAsync);
    }

    private void OnOpenTrainBooking(
        object sender,
        RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        if (session.CurrentUser?.Role != "Tourist")
        {
            MessageBox.Show(
                this,
                "Для бронювання увійдіть в акаунт туриста.",
                "Smart Trip",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var window = new BookingCenterWindow(
            trainBooking,
            housingBooking,
            carBooking,
            excursionBooking,
            trips,
            session,
            logger)
        {
            Owner = this
        };

        window.ShowDialog();
    }
}