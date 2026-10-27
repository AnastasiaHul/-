using System.Windows;
using System.Windows.Controls;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class MainWindow
{
    private async void OnAddPlaceExpense(
        object sender,
        RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        Place? selectedPlace = null;
        IReadOnlyList<Trip>? availableTrips = null;

        await ExecuteAsync(async () =>
        {
            session.RequireRole("Tourist");

            selectedPlace =
                UiInput.Selected<Place>(CatalogGrid);

            availableTrips = await trips.ListAsync();

            if (availableTrips.Count == 0)
            {
                throw new ArgumentException(
                    "Спочатку створіть подорож "
                    + "у розділі «Мої подорожі».");
            }
        });

        if (selectedPlace is null
            || availableTrips is null
            || availableTrips.Count == 0)
        {
            return;
        }

        Place place = selectedPlace;

        var form = new StackPanel();

        form.Children.Add(new TextBlock
        {
            Text = $"{place.Name}\n"
                + $"Ціна одного відвідування: {place.Price:N2} ₴",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 18,
            Margin = new Thickness(0, 0, 0, 20)
        });

        form.Children.Add(new TextBlock
        {
            Text = "До якої подорожі додати витрату?"
        });

        var tripBox = new ComboBox
        {
            ItemsSource = availableTrips,
            DisplayMemberPath = nameof(Trip.Title),
            SelectedIndex = 0,
            Margin = new Thickness(0, 8, 0, 16)
        };

        form.Children.Add(tripBox);

        form.Children.Add(new TextBlock
        {
            Text = "Дата відвідування"
        });

        var datePicker = new DatePicker
        {
            Margin = new Thickness(0, 8, 0, 16)
        };

        form.Children.Add(datePicker);

        void UpdateDate()
        {
            if (tripBox.SelectedItem is Trip selectedTrip)
            {
                datePicker.DisplayDateStart =
                    selectedTrip.StartDate.Date;

                datePicker.DisplayDateEnd =
                    selectedTrip.EndDate.Date;

                datePicker.SelectedDate =
                    selectedTrip.StartDate.Date;
            }
        }

        tripBox.SelectionChanged += (_, _) => UpdateDate();
        UpdateDate();

        form.Children.Add(new TextBlock
        {
            Text = "Після підтвердження ціна місця "
                + "буде додана до витрат вибраної подорожі "
                + "та зменшить залишок бюджету.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var saveButton = new Button
        {
            Content = "Підтвердити та додати до витрат"
        };

        form.Children.Add(saveButton);

        var dialog = CreateDialog(
            "Додати місце до витрат",
            form,
            600);

        saveButton.Click += async (_, _) =>
        {
            await ExecuteAsync(async () =>
            {
                if (tripBox.SelectedItem is not Trip selectedTrip)
                {
                    throw new ArgumentException(
                        "Виберіть подорож.");
                }

                if (datePicker.SelectedDate is not DateTime date)
                {
                    throw new ArgumentException(
                        "Виберіть дату відвідування.");
                }

                await budget.AddPlaceExpenseAsync(
                    selectedTrip.TripId,
                    place.PlaceId,
                    date);

                // Не дозволяємо повторне збереження
                // тієї самої форми після успішного запису.
                saveButton.IsEnabled = false;

                await RefreshTripsAsync();

                BudgetTrip.SelectedItem =
                    availableTrips.First(
                        trip => trip.TripId == selectedTrip.TripId);

                // Знаходимо подорож серед оновлених об'єктів.
                BudgetTrip.SelectedItem =
                    BudgetTrip.Items
                        .OfType<Trip>()
                        .First(trip =>
                            trip.TripId == selectedTrip.TripId);

                await RefreshBudgetAsync();

                NavigateTo(3);
                CompleteDialog();
            });
        };

        activeDialog = dialog;
        allowDialogClose = false;

        dialog.Closing += (_, args) =>
            args.Cancel = busy && !allowDialogClose;

        try
        {
            dialog.ShowDialog();
        }
        finally
        {
            activeDialog = null;
            allowDialogClose = false;
        }
    }
}