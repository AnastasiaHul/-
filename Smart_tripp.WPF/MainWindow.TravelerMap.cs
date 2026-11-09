using System.Windows;
using SmartTrip.Application.Services;

namespace Smart_tripp.WPF;

public partial class MainWindow
{
    private readonly TravelerMapService travelerMap;
    private readonly BookingReadinessService readiness;

    private void OnOpenTravelerMap(object sender, RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        var access = session.TryRequireRole("Tourist");
        if (access.IsFailure)
        {
            logger.Information($"Map.Open.ValidationFailed: {access.Error}", session.CurrentUser?.UserId);
            MessageBox.Show(this, access.Error, "Карта мандрівника");
            return;
        }

        new TravelerMapWindow(travelerMap, trips, session, logger) { Owner = this }.ShowDialog();
    }
}
