using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace Smart_tripp.WPF;

public sealed class TravelerMapWindow : TravelToolWindow
{
    private readonly TravelerMapService map;
    private readonly ComboBox tripBox = new() { DisplayMemberPath = nameof(Trip.Title) };
    private readonly ComboBox locationBox = new() { DisplayMemberPath = nameof(Place.Name) };
    private readonly TextBox caption = new() { MaxLength = 120 };
    private readonly TextBox category = new() { Text = "Кав’ярні", MaxLength = 80 };
    private readonly ListBox photoList = new() { SelectionMode = SelectionMode.Extended, MinHeight = 105, MaxHeight = 150 };
    private readonly Image preview = new() { Width = 240, Height = 145, Stretch = Stretch.Uniform };
    private readonly TextBlock photoFile = new() { Text = "Фото не вибрано.", TextWrapping = TextWrapping.Wrap };
    private byte[]? pendingPhoto;

    public TravelerMapWindow(TravelerMapService map, TripService trips, UserSession session, IAppLogger logger)
        : base("Карта мандрівника", 860, 690, session, logger)
    {
        this.map = map;
        AddField(Form, "Подорож", tripBox);
        AddField(Form, "Місце з каталогу з повною адресою", locationBox);

        var tabs = new TabControl { Margin = new Thickness(0, 14, 0, 0) };
        Form.Children.Add(tabs);
        tabs.Items.Add(BuildPhotoTab());
        tabs.Items.Add(BuildNearbyTab());
        tabs.Items.Add(BuildRouteTab());

        tripBox.SelectionChanged += async (_, _) => await RunAsync("Map.ViewPhotos", LoadPhotosAsync);
        Loaded += async (_, _) => await RunAsync("Map.Open", async () =>
        {
            var tripResult = await trips.ListAsync();
            var placeResult = await map.ListPlacesAsync();
            if (tripResult.IsFailure || placeResult.IsFailure)
            {
                ShowStatus(tripResult.IsFailure ? tripResult.Error : placeResult.Error);
                return;
            }

            tripBox.ItemsSource = tripResult.Value;
            locationBox.ItemsSource = placeResult.Value;
            tripBox.SelectedIndex = tripResult.Value!.Count > 0 ? 0 : -1;
            locationBox.SelectedIndex = placeResult.Value!.Count > 0 ? 0 : -1;
            await LoadPhotosAsync();
        });
    }

    private TabItem BuildPhotoTab()
    {
        var panel = new StackPanel { Margin = new Thickness(14) };
        AddField(panel, "Підпис", caption);
        var choose = MakeButton("Вибрати JPG або PNG");
        choose.Click += async (_, _) => await RunAsync("Map.AddPhoto", PickPhotoAsync);
        panel.Children.Add(choose);
        panel.Children.Add(photoFile);
        var save = MakeButton("Додати фото на карту");
        save.Click += async (_, _) => await RunAsync("Map.AddPhoto", SavePhotoAsync);
        panel.Children.Add(save);
        return new TabItem { Header = "Додати фото", Content = panel };
    }

    private TabItem BuildNearbyTab()
    {
        var panel = new StackPanel { Margin = new Thickness(14) };
        AddField(panel, "Категорія, наприклад «Кав’ярні»", category);
        var find = MakeButton("Знайти місця поруч");
        find.Click += async (_, _) => await RunAsync("Map.FindNearby", FindNearbyAsync);
        panel.Children.Add(find);
        return new TabItem { Header = "Місця поруч", Content = panel };
    }

    private TabItem BuildRouteTab()
    {
        var panel = new StackPanel { Margin = new Thickness(14) };
        AddField(panel, "Вибери 2–5 фото через Ctrl. Маршрут іде в порядку списку.", photoList);
        photoList.SelectionChanged += (_, _) =>
        {
            preview.Source = photoList.SelectedItem is PhotoItem item ? Decode(item.Photo.Image) : null;
        };
        panel.Children.Add(preview);

        var buttons = new WrapPanel();
        var open = MakeButton("Показати місце фото");
        open.Click += async (_, _) => await RunAsync("Map.OpenPhotoPin", () =>
        {
            if (photoList.SelectedItem is not PhotoItem item)
            {
                Reject("Map.OpenPhotoPin", "Вибери фото.");
            }
            else
            {
                OpenMap(TravelerMapService.MapSearch(item.Photo.Address));
            }

            return Task.CompletedTask;
        });
        var route = MakeButton("Побудувати маршрут за фото");
        route.Click += async (_, _) => await RunAsync("Map.BuildPhotoRoute", BuildRouteAsync);
        buttons.Children.Add(open);
        buttons.Children.Add(route);
        panel.Children.Add(buttons);
        return new TabItem { Header = "Фото й маршрут", Content = panel };
    }

    private async Task PickPhotoAsync()
    {
        const string action = "Map.AddPhoto";
        var dialog = new OpenFileDialog { Filter = "Фото JPG і PNG|*.jpg;*.jpeg;*.png" };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        pendingPhoto = null;
        photoFile.Text = "Фото не вибрано.";
        await using var stream = File.OpenRead(dialog.FileName);
        if (stream.Length is <= 0 or > TravelerMapService.MaxPhotoBytes)
        {
            Reject(action, "Вибери фото розміром до 3 МБ.");
            return;
        }

        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes);
        try
        {
            _ = Decode(bytes);
        }
        catch (Exception exception) when (exception is NotSupportedException or FileFormatException
            or ArgumentException or InvalidOperationException or IOException)
        {
            Reject(action, "Файл не вдалося прочитати як зображення.");
            return;
        }

        pendingPhoto = bytes;
        photoFile.Text = Path.GetFileName(dialog.FileName);
        ShowStatus("Фото вибрано.");
    }

    private async Task SavePhotoAsync()
    {
        if (tripBox.SelectedItem is not Trip trip || locationBox.SelectedItem is not Place place
            || pendingPhoto is not byte[] bytes)
        {
            Reject("Map.AddPhoto", "Вибери подорож, місце та фото.");
            return;
        }

        var result = await map.AddPhotoAsync(trip.TripId, place.PlaceId, caption.Text, bytes);
        if (result.IsFailure)
        {
            ShowStatus(result.Error);
            return;
        }

        pendingPhoto = null;
        caption.Clear();
        photoFile.Text = "Фото не вибрано.";
        await LoadPhotosAsync();
        ShowStatus("Фото збережено. Його позначку можна відкрити у вкладці «Фото й маршрут».");
    }

    private async Task FindNearbyAsync()
    {
        int placeId = locationBox.SelectedItem is Place place ? place.PlaceId : 0;
        var result = await map.FindNearbyAsync(placeId, category.Text);
        if (result.IsFailure)
        {
            ShowStatus(result.Error);
            return;
        }

        OpenMap(result.Value!);
        ShowStatus("Пошук поруч відкрито в Google Maps.");
    }

    private async Task BuildRouteAsync()
    {
        if (tripBox.SelectedItem is not Trip trip)
        {
            Reject("Map.BuildPhotoRoute", "Вибери подорож.");
            return;
        }

        int[] ids = photoList.SelectedItems.OfType<PhotoItem>().Select(x => x.Photo.TripPhotoId)
            .Order().ToArray();
        var result = await map.BuildPhotoRouteAsync(trip.TripId, ids);
        if (result.IsFailure)
        {
            ShowStatus(result.Error);
            return;
        }

        OpenMap(result.Value!);
        ShowStatus("Маршрут відкрито в Google Maps.");
    }

    private async Task LoadPhotosAsync()
    {
        photoList.ItemsSource = null;
        preview.Source = null;
        if (tripBox.SelectedItem is not Trip trip)
        {
            ShowStatus("Спочатку створи подорож.");
            return;
        }

        var result = await map.ListPhotosAsync(trip.TripId);
        if (result.IsFailure)
        {
            ShowStatus(result.Error);
            return;
        }

        photoList.ItemsSource = result.Value!.Select(x => new PhotoItem(x)).ToList();
        ShowStatus($"Фото у вибраній подорожі: {result.Value!.Count}.");
    }

    private static BitmapImage Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 360;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static void OpenMap(string url) =>
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

    private sealed record PhotoItem(TripPhoto Photo)
    {
        public override string ToString() => $"{Photo.Caption} — {Photo.PlaceName}";
    }
}
