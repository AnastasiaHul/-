using System.Windows;
using System.Windows.Controls;
using SmartTrip.Application.Common;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;
public partial class MainWindow
{
    private async void OnOpenTripWorkspace(object sender, RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        int? selectedId = null;
        await ExecuteAsync(() =>
        {
            Result<User> access = session.TryRequireRole("Tourist");
            if (access.IsFailure)
            {
                ShowValidationError(access.Error, log: true);
                return Task.CompletedTask;
            }

            if (TripsGrid.SelectedItem is not Trip selected)
            {
                ShowValidationError("Спочатку виберіть подорож у таблиці.", log: true);
                return Task.CompletedTask;
            }

            selectedId = selected.TripId;
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
                Result<IReadOnlyList<Trip>> result = await trips.ListAsync();
                if (result.IsFailure)
                {
                    ShowValidationError(result.Error);
                    return;
                }

                selectedTrip = result.Value!.FirstOrDefault(trip => trip.TripId == tripId);
                if (selectedTrip is null)
                {
                    ShowValidationError("Подорож не знайдено.", log: true);
                }
            });
            if (selectedTrip is null)
            {
                return;
            }

            var workspace = new TripWorkspaceWindow(selectedTrip, trips, itinerary, budget, packing, mapRoutes, session, logger, selectedTab)
            {
                Owner = this
            };
            workspace.ShowDialog();
            selectedTab = workspace.SelectedTabIndex;
            await ExecuteAsync(RefreshTripsAsync);
            if (validationFailed)
            {
                return;
            }

            TripsGrid.SelectedItem = TripsGrid.Items.OfType<Trip>().FirstOrDefault(trip => trip.TripId == tripId);
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
                OnOpenDialog(editButton, new RoutedEventArgs());
                continue;
            }

            return;
        }
    }
}
