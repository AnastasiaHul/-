using System.Windows;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;

namespace Smart_tripp.WPF;

public partial class BookingCenterWindow : Window
{
    public BookingCenterWindow(
        TrainBookingService trains,
        AccommodationBookingService housing,
        CarBookingService cars,
        ExcursionBookingService excursions,
        TripService trips,
        UserSession session,
        IAppLogger logger)
    {
        InitializeComponent();

        BackButton.Click += (_, _) => Close();

        OpenTrainsButton.Click += (_, _) =>
        {
            var window = new TrainBookingWindow(
                trains,
                trips,
                session,
                logger)
            {
                Owner = this
            };

            window.ShowDialog();
        };

        OpenHousingButton.Click += (_, _) =>
        {
            var window = new AccommodationBookingWindow(
                housing,
                trips,
                session,
                logger)
            {
                Owner = this
            };

            window.ShowDialog();
        };

        OpenCarsButton.Click += (_, _) =>
        {
            var window = new CarBookingWindow(
                cars,
                trips,
                session,
                logger)
            {
                Owner = this
            };

            window.ShowDialog();
        };

        OpenExcursionsButton.Click += (_, _) =>
        {
            var window = new ExcursionBookingWindow(
                excursions,
                trips,
                session,
                logger)
            {
                Owner = this
            };

            window.ShowDialog();
        };
    }
}