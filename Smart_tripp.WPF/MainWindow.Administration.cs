using System.Windows;
using SmartTrip.Application.Services;

namespace Smart_tripp.WPF;

public partial class MainWindow
{
    private readonly AdministrationService administration;

    private void OnOpenAdministration(object sender, RoutedEventArgs e)
    {
        if (busy || activeDialog is not null || session.CurrentUser?.Role != "Admin")
        {
            return;
        }

        new AdministrationWindow(administration, session, logger) { Owner = this }.ShowDialog();
    }
}
