using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;
public partial class MainWindow
{
    private async void OnOpenRouteMap(object sender, RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        Trip? selectedTrip = null;
        await ExecuteAsync(() =>
        {
            var check1 = session.TryRequireRole("Tourist");
            if (check1.IsFailure)
            {
                ShowValidationError(check1.Error, log: true);
                return Task.CompletedTask;
            }

            var check2 = UiInput.Selected<Trip>(TripsGrid);
            if (check2.IsFailure)
            {
                ShowValidationError(check2.Error, log: true);
                return Task.CompletedTask;
            }

            selectedTrip = check2.Value!;
            return Task.CompletedTask;
        });
        if (selectedTrip is null)
        {
            return;
        }

        Trip trip = selectedTrip;
        var form = new StackPanel();
        form.Children.Add(new TextBlock { Text = trip.Title, FontSize = 20, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        form.Children.Add(new TextBlock { Text = "Виберіть день подорожі" });
        var dayBox = new DatePicker
        {
            DisplayDateStart = trip.StartDate.Date,
            DisplayDateEnd = trip.EndDate.Date,
            SelectedDate = trip.StartDate.Date,
            Margin = new Thickness(0, 8, 0, 16)
        };
        form.Children.Add(dayBox);
        form.Children.Add(new TextBlock { Text = "Як будете пересуватися?" });
        var modeBox = new ComboBox
        {
            Margin = new Thickness(0, 8, 0, 16)
        };
        modeBox.Items.Add(new ComboBoxItem { Content = "Пішки", Tag = "walking" });
        modeBox.Items.Add(new ComboBoxItem { Content = "Автомобілем", Tag = "driving" });
        modeBox.SelectedIndex = 0;
        form.Children.Add(modeBox);
        form.Children.Add(new TextBlock { Text = "Маршрут включатиме місця цього дня " + "в порядку запланованого часу. " + "Для побудови потрібні щонайменше " + "два відвідування з повними адресами. " + "Карта відкриється у браузері; " + "потрібне підключення до інтернету.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) });
        var openButton = new Button
        {
            Content = "Показати маршрут на карті"
        };
        form.Children.Add(openButton);
        openButton.Click += async (_, _) =>
        {
            await ExecuteAsync(async () =>
            {
                var check3 = UiInput.Date(dayBox);
                if (check3.IsFailure)
                {
                    ShowValidationError(check3.Error, log: true);
                    return;
                }

                DateTime day = check3.Value!;
                if (modeBox.SelectedItem is not ComboBoxItem selectedMode || selectedMode.Tag is not string mode)
                {
                    ShowValidationError("Виберіть спосіб пересування.", log: true);
                    return;
                }

                var check4 = await mapRoutes.BuildUrlAsync(trip.TripId, day, mode);
                if (check4.IsFailure)
                {
                    ShowValidationError(check4.Error, log: false);
                    return;
                }

                string url = check4.Value!;
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                catch (Win32Exception exception)
                {
                    throw new InvalidOperationException("Не вдалося відкрити браузер. " + "Перевірте налаштування браузера " + "за замовчуванням у Windows.", exception);
                }

                logger.Information("OpenTripRouteMap", session.CurrentUser?.UserId);
                CompleteDialog();
            });
        };
        ShowItineraryDialog(form, "Маршрут подорожі", 650);
    }
}
