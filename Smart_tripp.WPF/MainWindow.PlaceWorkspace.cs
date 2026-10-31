using System.Windows;
using System.Windows.Controls;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class MainWindow
{
    private async void OnOpenPlaceWorkspace(
        object sender,
        RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        Place? selectedPlace = null;

        await ExecuteAsync(() =>
        {
            selectedPlace =
                UiInput.Selected<Place>(CatalogGrid);

            return Task.CompletedTask;
        });

        if (selectedPlace is null)
        {
            return;
        }

        var workspace = new PlaceWorkspaceWindow(
            selectedPlace.PlaceId,
            catalog,
            session,
            logger)
        {
            Owner = this
        };

        workspace.ShowDialog();

        switch (workspace.RequestedAction)
        {
            case "PlanVisit":
                OnPlanVisit(
                    new Button(),
                    new RoutedEventArgs());

                return;

            case "Expense":
                OnAddPlaceExpense(
                    new Button(),
                    new RoutedEventArgs());

                return;

            case "Login":
                OpenAuthentication(false);

                NavigateTo(2);

                await ExecuteAsync(SearchAsync);
                return;

            default:
                await ExecuteAsync(SearchAsync);
                return;
        }
    }
}