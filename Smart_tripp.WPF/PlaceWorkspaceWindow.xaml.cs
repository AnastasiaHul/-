using System.Globalization;
using System.Windows;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;

namespace Smart_tripp.WPF;
public partial class PlaceWorkspaceWindow : Window
{
    private readonly int placeId;
    private readonly CatalogService catalog;
    private readonly UserSession session;
    private readonly IAppLogger logger;
    private bool busy;
    private bool validationFailed;
    public string? RequestedAction { get; private set; }
    public int SelectedTabIndex => PlaceTabs.SelectedIndex;

    public PlaceWorkspaceWindow(int placeId, CatalogService catalog, UserSession session, IAppLogger logger, int selectedTab = 0)
    {
        this.placeId = placeId;
        this.catalog = catalog;
        this.session = session;
        this.logger = logger;
        InitializeComponent();
        PlaceTabs.SelectedIndex = selectedTab;
        bool tourist = session.CurrentUser?.Role == "Tourist";
        FavoriteButton.IsEnabled = tourist;
        TouristActionsPanel.Visibility = tourist ? Visibility.Visible : Visibility.Collapsed;
        TravelAccessHint.Visibility = tourist ? Visibility.Collapsed : Visibility.Visible;
        SignInButton.Visibility = session.CurrentUser is null ? Visibility.Visible : Visibility.Collapsed;
        ReviewEditorExpander.Visibility = Visibility.Collapsed;
        BackButton.Click += (_, _) => Close();
        SignInButton.Click += (_, _) => Finish("Login");
        PlanVisitButton.Click += (_, _) => Finish("PlanVisit");
        AddPlaceExpenseButton.Click += (_, _) => Finish("Expense");
        FavoriteButton.Click += OnToggleFavorite;
        SaveReviewButton.Click += OnSaveReview;
        Closing += (_, args) => args.Cancel = busy;
        Loaded += async (_, _) => await ExecuteAsync(RefreshAsync);
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        validationFailed = false;
        IsEnabled = false;
        PlaceStatusText.Text = "Завантаження…";
        try
        {
            await action();
            if (!validationFailed) { PlaceStatusText.Text = "Готово. Інформацію про місце оновлено."; }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "PlaceWorkspaceAction", session.CurrentUser?.UserId);
            string message = exception is ArgumentException or UnauthorizedAccessException ? exception.Message : "Не вдалося виконати дію. " + "Деталі збережено в журналі.";
            PlaceStatusText.Text = message;
            MessageBox.Show(this, message, "Smart Trip", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
            busy = false;
        }
    }

    private async Task RefreshAsync()
    {
        var check1 = await catalog.DetailsAsync(placeId);
        if (check1.IsFailure)
        {
            ShowValidationError(check1.Error, log: false);
            return;
        }

        PlaceDetails details = check1.Value!;
        DataContext = details.Place;
        Title = $"{details.Place.Name} — Smart Trip";
        AverageRatingText.Text = details.AverageRating?.ToString("0.##", CultureInfo.CurrentCulture) ?? "—";
        ReviewCountText.Text = details.Reviews.Count == 0 ? "Відгуків ще немає" : $"Відгуків: {details.Reviews.Count}";
        PlaceReviewsGrid.ItemsSource = details.Reviews.OrderByDescending(review => review.CreatedAt).ToList();
        bool tourist = session.CurrentUser?.Role == "Tourist";
        if (!tourist)
        {
            ReviewEditorExpander.Visibility = Visibility.Collapsed;
            ReviewsAccessHint.Visibility = Visibility.Visible;
            ReviewsAccessHint.Text = "Увійдіть як турист, щоб залишити відгук.";
            return;
        }

        int userId = session.CurrentUser!.UserId;
        bool alreadyReviewed = details.Reviews.Any(review => review.UserId == userId);
        ReviewEditorExpander.Visibility = alreadyReviewed ? Visibility.Collapsed : Visibility.Visible;
        SaveReviewButton.IsEnabled = !alreadyReviewed;
        ReviewsAccessHint.Visibility = alreadyReviewed ? Visibility.Visible : Visibility.Collapsed;
        ReviewsAccessHint.Text = "Ви вже залишили відгук про це місце.";
        var check2 = await catalog.SearchAsync(string.Empty, string.Empty, null, true);
        if (check2.IsFailure)
        {
            ShowValidationError(check2.Error, log: false);
            return;
        }

        var favorites = check2.Value!;
        bool isFavorite = favorites.Any(place => place.PlaceId == placeId);
        FavoriteButton.Content = isFavorite ? "♥ Прибрати з обраного" : "♡ Зберегти в обране";
    }

    private async void OnToggleFavorite(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            var check3 = await catalog.ToggleFavoriteAsync(placeId);
            if (check3.IsFailure)
            {
                ShowValidationError(check3.Error, log: false);
                return;
            }

            await RefreshAsync();
        });
    }

    private async void OnSaveReview(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(async () =>
        {
            var check4 = await catalog.AddReviewAsync(placeId, ReviewRatingBox.SelectedIndex + 1, ReviewCommentBox.Text);
            if (check4.IsFailure)
            {
                ShowValidationError(check4.Error, log: false);
                return;
            }

            SaveReviewButton.IsEnabled = false;
            ReviewCommentBox.Clear();
            await RefreshAsync();
        });
    }

    private void Finish(string action)
    {
        if (busy)
        {
            return;
        }

        RequestedAction = action;
        Close();
    }

    private void ShowValidationError(string message, bool log = false)
    {
        validationFailed = true;
        PlaceStatusText.Text = message;
        if (log) logger.Information("PlaceWorkspaceWindow.ValidationFailed: " + message, session.CurrentUser?.UserId);
        MessageBox.Show(this, message, "Перевірте введені дані", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
