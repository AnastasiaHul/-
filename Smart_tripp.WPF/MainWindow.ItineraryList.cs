using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;
public partial class MainWindow
{
    private async void OnViewItinerary(object sender, RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        Trip? selectedTrip = null;
        IReadOnlyList<ItineraryVisit>? visits = null;
        await ExecuteAsync(async () =>
        {
            var check1 = UiInput.Selected<Trip>(TripsGrid);
            if (check1.IsFailure)
            {
                ShowValidationError(check1.Error, log: true);
                return;
            }

            selectedTrip = check1.Value!;
            var check2 = await itinerary.ListAsync(selectedTrip.TripId);
            if (check2.IsFailure)
            {
                ShowValidationError(check2.Error, log: false);
                return;
            }

            visits = check2.Value!;
        });
        if (selectedTrip is null || visits is null)
        {
            return;
        }

        Trip trip = selectedTrip;
        var form = new StackPanel();
        form.Children.Add(new TextBlock { Text = $"План подорожі: {trip.Title}", FontSize = 20, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        form.Children.Add(new TextBlock { Text = "Додавайте місця через каталог кнопкою " + "«Запланувати відвідування».", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            Height = 300,
            ItemsSource = visits
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Дата і час", Binding = new Binding(nameof(ItineraryVisit.VisitAt)) { StringFormat = "dd.MM.yyyy HH:mm" }, Width = new DataGridLength(150) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Місце", Binding = new Binding(nameof(ItineraryVisit.PlaceName)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Примітка", Binding = new Binding(nameof(ItineraryVisit.Notes)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        form.Children.Add(grid);
        var removeButton = new Button
        {
            Content = "Прибрати вибране відвідування",
            Margin = new Thickness(0, 16, 0, 0)
        };
        removeButton.Click += async (_, _) =>
        {
            await ExecuteAsync(async () =>
            {
                var check3 = UiInput.Selected<ItineraryVisit>(grid);
                if (check3.IsFailure)
                {
                    ShowValidationError(check3.Error, log: true);
                    return;
                }

                ItineraryVisit visit = check3.Value!;
                MessageBoxResult answer = MessageBox.Show(activeDialog ?? this, $"Прибрати «{visit.PlaceName}» з плану?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }

                var check4 = await itinerary.RemoveAsync(visit.ItineraryVisitId);
                if (check4.IsFailure)
                {
                    ShowValidationError(check4.Error, log: false);
                    return;
                }

                var check5 = await itinerary.ListAsync(trip.TripId);
                if (check5.IsFailure)
                {
                    ShowValidationError(check5.Error, log: false);
                    return;
                }

                grid.ItemsSource = check5.Value!;
            });
        };
        form.Children.Add(removeButton);
        ShowItineraryDialog(form, "План відвідувань", 900);
    }
}
