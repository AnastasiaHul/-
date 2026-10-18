using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public partial class MainWindow
{
    private readonly Stack<int> navigationHistory = new();
    private Window? activeDialog;
    private bool allowDialogClose;

    private void NavigateTo(int index)
    {
        if (Tabs.SelectedIndex != index)
        {
            navigationHistory.Push(Tabs.SelectedIndex);
        }

        Tabs.SelectedIndex = index;
        UpdatePageTitle();
    }

    private void UpdatePageTitle()
    {
        PageTitle.Text = Tabs.SelectedIndex switch
        {
            0 => "Мої подорожі",
            1 => "Місця партнера",
            2 => OnlyFavorites.IsChecked == true
                ? "Мої обрані місця"
                : "Каталог місць",
            3 => "Бюджет та список речей",
            _ => "Відкривайте світ зі Smart Trip"
        };
    }

    private void UpdateAccountNavigation()
    {
        bool tourist = session.CurrentUser?.Role == "Tourist";

        TripsNav.IsEnabled = tourist;
        BudgetNav.IsEnabled = tourist;

        PlacesNav.IsEnabled =
            session.CurrentUser?.Role == "Partner";

        LoginNav.Visibility = session.CurrentUser is null
            ? Visibility.Visible
            : Visibility.Collapsed;

        RegisterNav.Visibility = LoginNav.Visibility;

        navigationHistory.Clear();
        UpdatePageTitle();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        Tabs.SelectedIndex = navigationHistory.Count > 0
            ? navigationHistory.Pop()
            : 4;

        UpdatePageTitle();
    }

    private async void OnNavigate(object sender, RoutedEventArgs e)
    {
        string target = UiInput.Action(sender);

        if ((target is "Trips" or "Budget" or "Favorites")
            && session.CurrentUser?.Role != "Tourist")
        {
            MessageBox.Show(
                this,
                "Увійдіть як турист, щоб відкрити цей розділ.",
                "Smart Trip");

            OpenAuthentication(false);
            return;
        }

        if (target == "Places"
            && session.CurrentUser?.Role != "Partner")
        {
            return;
        }

        if (target is "Catalog" or "Favorites")
        {
            OnlyFavorites.IsChecked = target == "Favorites";

            NavigateTo(2);
            await ExecuteAsync(SearchAsync);
            return;
        }

        NavigateTo(target switch
        {
            "Trips" => 0,
            "Places" => 1,
            "Budget" => 3,
            _ => 4
        });

        if (target == "Budget"
            && TripsGrid.SelectedItem is Trip trip)
        {
            BudgetTrip.SelectedItem =
                (BudgetTrip.ItemsSource as IEnumerable<Trip>)?
                .FirstOrDefault(item => item.TripId == trip.TripId);
        }
    }

    private async void OnDestination(
        object sender,
        RoutedEventArgs e)
    {
        string destination = UiInput.Action(sender);

        SearchText.Clear();
        SearchCategory.Clear();
        SearchPrice.Clear();
        OnlyFavorites.IsChecked = false;

        NavigateTo(2);

        await ExecuteAsync(async () =>
        {
            IReadOnlyList<Place> all =
                await catalog.SearchAsync(
                    string.Empty,
                    string.Empty,
                    null,
                    false);

            CatalogGrid.ItemsSource = all
                .Where(place =>
                    (place.Address?.Contains(
                        destination,
                        StringComparison.OrdinalIgnoreCase) ?? false)
                    || place.Name.Contains(
                        destination,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            PageTitle.Text = $"Каталог · {destination}";
        });
    }

    private void OnOpenDialog(
        object sender,
        RoutedEventArgs e)
    {
        if (busy || activeDialog is not null)
        {
            return;
        }

        string action = UiInput.Action(sender);

        if (action is "Login" or "Register")
        {
            OpenAuthentication(action == "Register");
            return;
        }

        if ((action is "NewTrip" or "EditTrip")
            && session.CurrentUser?.Role != "Tourist")
        {
            OpenAuthentication(false);
            return;
        }

        if ((action is "NewPlace" or "EditPlace")
            && session.CurrentUser?.Role != "Partner")
        {
            return;
        }

        if ((action is "Expense" or "Statistics")
            && BudgetTrip.SelectedItem is not Trip)
        {
            MessageBox.Show(
                this,
                "Спочатку створіть або виберіть подорож.",
                "Smart Trip");

            return;
        }

        switch (action)
        {
            case "NewTrip":
                NavigateTo(0);
                TripsGrid.SelectedItem = null;
                ClearTripEditor();

                ShowForm(
                    TripEditor,
                    "Створити нову подорож");

                break;

            case "EditTrip":
                if (TripsGrid.SelectedItem is not Trip)
                {
                    MessageBox.Show(
                        this,
                        "Виберіть подорож у таблиці.",
                        "Smart Trip");

                    return;
                }

                OnTripSelected(
                    TripsGrid,
                    new SelectionChangedEventArgs(
                        SelectorEvent(),
                        Array.Empty<object>(),
                        Array.Empty<object>()));

                ShowForm(
                    TripEditor,
                    "Редагувати подорож");

                break;

            case "NewPlace":
                PlacesGrid.SelectedItem = null;
                ClearPlaceEditor();

                ShowForm(
                    PlaceEditor,
                    "Додати нове місце");

                break;

            case "EditPlace":
                if (PlacesGrid.SelectedItem is not Place)
                {
                    MessageBox.Show(
                        this,
                        "Виберіть місце у таблиці.",
                        "Smart Trip");

                    return;
                }

                OnPlaceSelected(
                    PlacesGrid,
                    new SelectionChangedEventArgs(
                        SelectorEvent(),
                        Array.Empty<object>(),
                        Array.Empty<object>()));

                ShowForm(
                    PlaceEditor,
                    "Редагувати місце");

                break;

            case "Review":
                if (session.CurrentUser?.Role != "Tourist")
                {
                    OpenAuthentication(false);
                    return;
                }

                if (CatalogGrid.SelectedItem is not Place selected)
                {
                    MessageBox.Show(
                        this,
                        "Виберіть місце в каталозі.",
                        "Smart Trip");

                    return;
                }

                ShowForm(
                    ReviewEditor,
                    $"Відгук · {selected.Name}");

                break;

            case "Expense":
                ShowForm(
                    ExpenseEditor,
                    "Додати витрату");

                break;

            case "Statistics":
                ShowForm(
                    StatisticsEditor,
                    "Статистика витрат за категоріями",
                    720);

                break;
        }
    }

    private static RoutedEvent SelectorEvent()
    {
        return System.Windows.Controls.Primitives
            .Selector.SelectionChangedEvent;
    }

    private void OpenAuthentication(bool register)
    {
        PasswordBox.Clear();

        RegistrationFields.Visibility = register
            ? Visibility.Visible
            : Visibility.Collapsed;

        RegisterSubmit.Visibility =
            RegistrationFields.Visibility;

        LoginSubmit.Visibility = register
            ? Visibility.Collapsed
            : Visibility.Visible;

        ShowForm(
            LoginPanel,
            register
                ? "Створити обліковий запис"
                : "Вхід у Smart Trip",
            520);

        PasswordBox.Clear();
    }

    private void ShowForm(
        FrameworkElement form,
        string title,
        double width = 650)
    {
        if (activeDialog is not null
            || form.Parent is not Panel parent)
        {
            return;
        }

        int index = parent.Children.IndexOf(form);

        parent.Children.Remove(form);
        form.Visibility = Visibility.Visible;

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
            var layout = (DockPanel)dialog.Content;
            var scrollViewer = (ScrollViewer)layout.Children[2];

            scrollViewer.Content = null;

            form.Visibility = Visibility.Collapsed;
            parent.Children.Insert(index, form);

            activeDialog = null;
            allowDialogClose = false;
        }
    }

    private Window CreateDialog(
        string title,
        UIElement content,
        double width)
    {
        var dialog = new Window
        {
            Title = $"{title} — Smart Trip",
            Owner = this,
            Width = width,
            Height = Math.Min(
                780,
                SystemParameters.WorkArea.Height - 50),
            MinWidth = 420,
            MinHeight = 400,
            Background = (Brush)FindResource("PageBrush"),
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner
        };

        var layout = new DockPanel
        {
            Margin = new Thickness(26)
        };

        var heading = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 20)
        };

        heading.Children.Add(new TextBlock
        {
            Text = "SMART TRIP",
            Foreground = (Brush)FindResource("AccentBrush"),
            FontWeight = FontWeights.Bold,
            FontSize = 12
        });

        heading.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 25,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        });

        DockPanel.SetDock(heading, Dock.Top);
        layout.Children.Add(heading);

        var back = new Button
        {
            Content = "← Назад",
            Style = (Style)FindResource("SecondaryButton"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 18, 0, 0)
        };

        back.Click += (_, _) => dialog.Close();

        DockPanel.SetDock(back, Dock.Bottom);
        layout.Children.Add(back);

        layout.Children.Add(new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility =
                ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility =
                ScrollBarVisibility.Disabled
        });

        dialog.Content = layout;

        return dialog;
    }

    private void CompleteDialog()
    {
        allowDialogClose = true;
        activeDialog?.Close();
    }
}