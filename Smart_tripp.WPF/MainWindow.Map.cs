using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class MainWindow
{
    private async void OnOpenRouteMap(
        object sender,
        RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        Trip? selectedTrip = null;

        await ExecuteAsync(() =>
        {
            session.RequireRole("Tourist");

            selectedTrip =
                UiInput.Selected<Trip>(TripsGrid);

            return Task.CompletedTask;
        });

        if (selectedTrip is null)
        {
            return;
        }

        Trip trip = selectedTrip;

        var form = new StackPanel();

        form.Children.Add(new TextBlock
        {
            Text = trip.Title,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        form.Children.Add(new TextBlock
        {
            Text = "Виберіть день подорожі"
        });

        var dayBox = new DatePicker
        {
            DisplayDateStart = trip.StartDate.Date,
            DisplayDateEnd = trip.EndDate.Date,
            SelectedDate = trip.StartDate.Date,
            Margin = new Thickness(0, 8, 0, 16)
        };

        form.Children.Add(dayBox);

        form.Children.Add(new TextBlock
        {
            Text = "Як будете пересуватися?"
        });

        var modeBox = new ComboBox
        {
            Margin = new Thickness(0, 8, 0, 16)
        };

        modeBox.Items.Add(new ComboBoxItem
        {
            Content = "Пішки",
            Tag = "walking"
        });

        modeBox.Items.Add(new ComboBoxItem
        {
            Content = "Автомобілем",
            Tag = "driving"
        });

        modeBox.SelectedIndex = 0;

        form.Children.Add(modeBox);

        form.Children.Add(new TextBlock
        {
            Text = "Маршрут включатиме місця цього дня "
                + "в порядку запланованого часу. "
                + "Для побудови потрібні щонайменше "
                + "два відвідування з повними адресами. "
                + "Карта відкриється у браузері; "
                + "потрібне підключення до інтернету.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        });

        var openButton = new Button
        {
            Content = "Показати маршрут на карті"
        };

        form.Children.Add(openButton);

        openButton.Click += async (_, _) =>
        {
            await ExecuteAsync(async () =>
            {
                DateTime day = UiInput.Date(dayBox);

                if (modeBox.SelectedItem
                    is not ComboBoxItem selectedMode
                    || selectedMode.Tag is not string mode)
                {
                    throw new ArgumentException(
                        "Виберіть спосіб пересування.");
                }

                string url = await mapRoutes.BuildUrlAsync(
                    trip.TripId,
                    day,
                    mode);

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Win32Exception exception)
                {
                    throw new ArgumentException(
                        "Не вдалося відкрити браузер. "
                        + "Перевірте налаштування браузера "
                        + "за замовчуванням у Windows.",
                        exception);
                }

                logger.Information(
                    "OpenTripRouteMap",
                    session.CurrentUser?.UserId);

                CompleteDialog();
            });
        };

        ShowItineraryDialog(
            form,
            "Маршрут подорожі",
            650);
    }
}