using System.Windows;
using System.Windows.Controls;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public sealed class BookingReadinessWindow : TravelToolWindow
{
    private readonly BookingReadinessService readiness;
    private readonly ComboBox tripBox = new() { DisplayMemberPath = nameof(Trip.Title) };
    private readonly CheckBox train = Need("Потяг");
    private readonly CheckBox housing = Need("Житло");
    private readonly CheckBox car = Need("Авто");
    private readonly CheckBox excursion = Need("Екскурсія");
    private readonly StackPanel report = new() { Margin = new Thickness(0, 10, 0, 0) };

    public BookingReadinessWindow(BookingReadinessService readiness, TripService trips,
        UserSession session, IAppLogger logger)
        : base("Готовність подорожі", 700, 490, session, logger)
    {
        this.readiness = readiness;
        AddField(Form, "Подорож", tripBox);
        var options = new WrapPanel();
        options.Children.Add(train);
        options.Children.Add(housing);
        options.Children.Add(car);
        options.Children.Add(excursion);
        AddField(Form, "Що потрібно для цієї подорожі?", options);

        var button = MakeButton("Перевірити готовність");
        button.Click += async (_, _) => await RunAsync("Booking.ReadinessCheck", CheckAsync);
        Form.Children.Add(button);
        Form.Children.Add(report);

        Loaded += async (_, _) => await RunAsync("Booking.ReadinessCheck.Open", async () =>
        {
            var result = await trips.ListAsync();
            if (result.IsFailure)
            {
                ShowStatus(result.Error);
                return;
            }

            tripBox.ItemsSource = result.Value;
            tripBox.SelectedIndex = result.Value!.Count > 0 ? 0 : -1;
            if (tripBox.SelectedIndex < 0)
            {
                ShowStatus("Спочатку створи подорож.");
            }
        });
    }

    private async Task CheckAsync()
    {
        report.Children.Clear();
        int tripId = tripBox.SelectedItem is Trip trip ? trip.TripId : 0;
        var result = await readiness.CheckAsync(tripId, new BookingNeeds(
            train.IsChecked == true, housing.IsChecked == true,
            car.IsChecked == true, excursion.IsChecked == true));
        if (result.IsFailure)
        {
            ShowStatus(result.Error);
            return;
        }

        BookingReadiness value = result.Value!;
        foreach (ReadinessItem item in value.Items)
        {
            string status = !item.Required ? "—" : item.Ready ? "✓" : "!";
            report.Children.Add(new TextBlock
            {
                Text = $"{status} {item.Category}: {item.Details}",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 9)
            });
        }

        ShowStatus($"Готово {value.ReadyCount} із {value.RequiredCount}."
            + (value.ReadyCount == value.RequiredCount ? " Усі вибрані бронювання готові." : ""));
    }

    private static CheckBox Need(string label) =>
        new() { Content = label, Margin = new Thickness(0, 4, 16, 4) };
}
