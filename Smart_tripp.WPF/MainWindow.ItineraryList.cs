using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class MainWindow
{
    private async void OnViewItinerary(
        object sender,
        RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        Trip? selectedTrip = null;
        IReadOnlyList<ItineraryVisit>? visits = null;

        await ExecuteAsync(async () =>
        {
            selectedTrip =
                UiInput.Selected<Trip>(TripsGrid);

            visits = await itinerary.ListAsync(
                selectedTrip.TripId);
        });

        if (selectedTrip is null || visits is null)
        {
            return;
        }

        Trip trip = selectedTrip;

        var form = new StackPanel();

        form.Children.Add(new TextBlock
        {
            Text = $"План подорожі: {trip.Title}",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        form.Children.Add(new TextBlock
        {
            Text = "Додавайте місця через каталог кнопкою "
                + "«Запланувати відвідування».",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            Height = 300,
            ItemsSource = visits
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Дата і час",
            Binding = new Binding(
                nameof(ItineraryVisit.VisitAt))
            {
                StringFormat = "dd.MM.yyyy HH:mm"
            },
            Width = new DataGridLength(150)
        });

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Місце",
            Binding = new Binding(
                nameof(ItineraryVisit.PlaceName)),
            Width = new DataGridLength(
                1,
                DataGridLengthUnitType.Star)
        });

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Примітка",
            Binding = new Binding(
                nameof(ItineraryVisit.Notes)),
            Width = new DataGridLength(
                1,
                DataGridLengthUnitType.Star)
        });

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
                ItineraryVisit visit =
                    UiInput.Selected<ItineraryVisit>(grid);

                MessageBoxResult answer = MessageBox.Show(
                    activeDialog ?? this,
                    $"Прибрати «{visit.PlaceName}» з плану?",
                    "Підтвердження",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }

                await itinerary.RemoveAsync(
                    visit.ItineraryVisitId);

                grid.ItemsSource =
                    await itinerary.ListAsync(trip.TripId);
            });
        };

        form.Children.Add(removeButton);

        ShowItineraryDialog(
            form,
            "План відвідувань",
            900);
    }
}