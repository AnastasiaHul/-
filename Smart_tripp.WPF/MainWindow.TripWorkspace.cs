using System.Windows;
using System.Windows.Controls;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class MainWindow
{
    private async void OnOpenTripWorkspace(
        object sender,
        RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        int? selectedId = null;

        await ExecuteAsync(() =>
        {
            session.RequireRole("Tourist");

            selectedId =
                UiInput.Selected<Trip>(TripsGrid).TripId;

            return Task.CompletedTask;
        });

        if (selectedId is not int tripId)
        {
            return;
        }

        int selectedTab = 0;

        while (true)
        {
            Trip? selectedTrip = null;

            await ExecuteAsync(async () =>
            {
                IReadOnlyList<Trip> ownTrips =
                    await trips.ListAsync();

                selectedTrip = ownTrips.FirstOrDefault(
                    trip => trip.TripId == tripId)
                    ?? throw new ArgumentException(
                        "Подорож не знайдено.");
            });

            if (selectedTrip is null)
            {
                return;
            }

            var workspace = new TripWorkspaceWindow(
                selectedTrip,
                trips,
                itinerary,
                budget,
                packing,
                mapRoutes,
                session,
                logger,
                selectedTab)
            {
                Owner = this
            };

            workspace.ShowDialog();

            selectedTab = workspace.SelectedTabIndex;

            await ExecuteAsync(RefreshTripsAsync);

            TripsGrid.SelectedItem =
                TripsGrid.Items
                    .OfType<Trip>()
                    .FirstOrDefault(
                        trip => trip.TripId == tripId);

            if (workspace.RequestedAction == "Catalog")
            {
                OnlyFavorites.IsChecked = false;

                NavigateTo(2);

                await ExecuteAsync(SearchAsync);
                return;
            }

            if (workspace.RequestedAction == "EditTrip")
            {
                var editButton = new Button
                {
                    Tag = "EditTrip"
                };

                OnOpenDialog(
                    editButton,
                    new RoutedEventArgs());

                // Після редагування знову відкриваємо
                // подорож з актуальними даними.
                continue;
            }

            return;
        }
    }
}