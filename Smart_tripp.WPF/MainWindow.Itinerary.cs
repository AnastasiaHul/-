using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class MainWindow
{
    private async void OnPlanVisit(
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
                    "Спочатку створіть подорож.");
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
            Text = place.Name,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        form.Children.Add(new TextBlock
        {
            Text = "Виберіть подорож"
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

        var dateBox = new DatePicker
        {
            Margin = new Thickness(0, 8, 0, 16)
        };

        form.Children.Add(dateBox);

        void UpdateDate()
        {
            if (tripBox.SelectedItem is Trip trip)
            {
                dateBox.SelectedDate = null;
                dateBox.DisplayDateStart = null;
                dateBox.DisplayDateEnd = null;

                dateBox.DisplayDateStart =
                    trip.StartDate.Date;

                dateBox.DisplayDateEnd =
                    trip.EndDate.Date;

                dateBox.SelectedDate =
                    trip.StartDate.Date;
            }
        }

        tripBox.SelectionChanged += (_, _) => UpdateDate();
        UpdateDate();

        form.Children.Add(new TextBlock
        {
            Text = "Місцевий час відвідування, наприклад 10:30"
        });

        var timeBox = new TextBox
        {
            Text = "10:00",
            Margin = new Thickness(0, 8, 0, 16)
        };

        form.Children.Add(timeBox);

        form.Children.Add(new TextBlock
        {
            Text = "Примітка — необов’язково"
        });

        var notesBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 70,
            MaxLength = 1000,
            Margin = new Thickness(0, 8, 0, 16)
        };

        form.Children.Add(notesBox);

        var saveButton = new Button
        {
            Content = "Додати до плану"
        };

        form.Children.Add(saveButton);

        saveButton.Click += async (_, _) =>
        {
            await ExecuteAsync(async () =>
            {
                if (tripBox.SelectedItem is not Trip trip)
                {
                    throw new ArgumentException(
                        "Виберіть подорож.");
                }

                DateTime date = UiInput.Date(dateBox);

                if (!TimeSpan.TryParseExact(
                    timeBox.Text.Trim(),
                    @"hh\:mm",
                    CultureInfo.InvariantCulture,
                    out TimeSpan time)
                    || time < TimeSpan.Zero
                    || time >= TimeSpan.FromDays(1))
                {
                    throw new ArgumentException(
                        "Вкажіть час у форматі 10:30.");
                }

                await itinerary.AddAsync(
                    trip.TripId,
                    place.PlaceId,
                    date.Date.Add(time),
                    notesBox.Text);

                saveButton.IsEnabled = false;
                CompleteDialog();
            });
        };

        ShowItineraryDialog(
            form,
            "Запланувати відвідування",
            650);
    }

    private void ShowItineraryDialog(
        UIElement form,
        string title,
        double width)
    {
        var dialog = CreateDialog(title, form, width);

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