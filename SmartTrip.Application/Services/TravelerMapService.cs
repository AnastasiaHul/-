using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class TravelerMapService(
    IRepository<TripPhoto> photos, IRepository<Place> places, TripService trips,
    UserSession session, IAppLogger logger)
{
    public const int MaxPhotoBytes = 3 * 1024 * 1024;

    public async Task<Result<TripPhoto>> AddPhotoAsync(
        int tripId, int placeId, string? caption, byte[] image)
    {
        const string action = "Map.AddPhoto";
        int? actor = session.CurrentUser?.UserId;
        try
        {
            var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            var title = ServiceOperations.TryRequired(caption, "Підпис", 120);
            string? error = owned.IsFailure ? owned.Error : title.IsFailure ? title.Error
                : !IsPhoto(image) ? "Виберіть JPG або PNG розміром до 3 МБ." : null;
            Place? place = error is null ? await places.FindAsync(placeId).ConfigureAwait(false) : null;
            if (error is null && !HasAddress(place))
            {
                error = "Виберіть доступне місце з повною адресою.";
            }

            if (error is not null)
            {
                logger.Information($"{action}.ValidationFailed: {error}", actor);
                return Result<TripPhoto>.Failure(error);
            }

            var photo = new TripPhoto
            {
                TripId = tripId, PlaceId = place!.PlaceId, PlaceName = place.Name,
                Address = place.Address!.Trim(), Caption = title.Value!,
                Image = (byte[])image.Clone(), CreatedAt = DateTime.UtcNow
            };
            await photos.AddAsync(photo).ConfigureAwait(false);
            logger.Information($"{action}.Succeeded: trip {tripId}, photo {photo.TripPhotoId}", actor);
            return photo;
        }
        catch (Exception exception)
        {
            logger.Error(exception, action, actor);
            throw;
        }
    }

    public async Task<Result<string>> FindNearbyAsync(int placeId, string? category)
    {
        const string action = "Map.FindNearby";
        int? actor = session.CurrentUser?.UserId;
        try
        {
            var access = session.TryRequireRole("Tourist");
            var validCategory = ServiceOperations.TryRequired(category, "Категорія", 80);
            string? error = access.IsFailure ? access.Error : validCategory.IsFailure ? validCategory.Error : null;
            Place? place = error is null ? await places.FindAsync(placeId).ConfigureAwait(false) : null;
            if (error is null && !HasAddress(place))
            {
                error = "Виберіть доступне місце з повною адресою.";
            }

            if (error is not null)
            {
                logger.Information($"{action}.ValidationFailed: {error}", actor);
                return Result<string>.Failure(error);
            }

            string url = MapSearch($"{validCategory.Value} near {place!.Address}");
            logger.Information($"{action}.Succeeded: place {placeId}", actor);
            return url;
        }
        catch (Exception exception)
        {
            logger.Error(exception, action, actor);
            throw;
        }
    }

    public async Task<Result<string>> BuildPhotoRouteAsync(int tripId, IReadOnlyList<int> photoIds)
    {
        const string action = "Map.BuildPhotoRoute";
        int? actor = session.CurrentUser?.UserId;
        try
        {
            var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (owned.IsFailure || photoIds.Count is < 2 or > 5)
            {
                string error = owned.IsFailure ? owned.Error : "Виберіть від 2 до 5 фото для маршруту.";
                logger.Information($"{action}.ValidationFailed: {error}", actor);
                return Result<string>.Failure(error);
            }

            var addresses = new List<string>();
            foreach (int id in photoIds)
            {
                TripPhoto? photo = await photos.FindAsync(id).ConfigureAwait(false);
                if (photo is null || photo.TripId != tripId)
                {
                    const string error = "Деякі фото не належать вибраній подорожі.";
                    logger.Information($"{action}.ValidationFailed: {error}", actor);
                    return Result<string>.Failure(error);
                }

                if (!addresses.Contains(photo.Address, StringComparer.OrdinalIgnoreCase))
                {
                    addresses.Add(photo.Address);
                }
            }

            if (addresses.Count < 2)
            {
                const string error = "Виберіть фото з двох різних місць.";
                logger.Information($"{action}.ValidationFailed: {error}", actor);
                return Result<string>.Failure(error);
            }

            string url = "https://www.google.com/maps/dir/?api=1"
                + $"&origin={Uri.EscapeDataString(addresses[0])}"
                + $"&destination={Uri.EscapeDataString(addresses[^1])}&travelmode=walking";
            if (addresses.Count > 2)
            {
                url += "&waypoints=" + Uri.EscapeDataString(string.Join("|", addresses.Skip(1).SkipLast(1)));
            }

            if (url.Length > 2048)
            {
                const string error = "Забагато адрес для посилання. Виберіть менше фото.";
                logger.Information($"{action}.ValidationFailed: {error}", actor);
                return Result<string>.Failure(error);
            }

            logger.Information($"{action}.Succeeded: trip {tripId}, stops {addresses.Count}", actor);
            return url;
        }
        catch (Exception exception)
        {
            logger.Error(exception, action, actor);
            throw;
        }
    }

    public async Task<Result<IReadOnlyList<TripPhoto>>> ListPhotosAsync(int tripId)
    {
        var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
        if (owned.IsFailure)
        {
            return new Failure(owned.Error);
        }

        var items = await photos.ListAsync(x => x.TripId == tripId).ConfigureAwait(false);
        return items.OrderBy(x => x.CreatedAt).ThenBy(x => x.TripPhotoId).ToList();
    }

    public async Task<Result<IReadOnlyList<Place>>> ListPlacesAsync()
    {
        var access = session.TryRequireRole("Tourist");
        if (access.IsFailure)
        {
            return new Failure(access.Error);
        }

        var items = await places.ListAsync(x => x.Status != "Hidden" && x.Address != null)
            .ConfigureAwait(false);
        return items.Where(HasAddress).OrderBy(x => x.Name).ToList();
    }

    public static string MapSearch(string query) =>
        "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(query);

    private static bool HasAddress(Place? place) =>
        place is { Status: not "Hidden" } && !string.IsNullOrWhiteSpace(place.Address);

    private static bool IsPhoto(byte[] image)
    {
        if (image.Length is < 8 or > MaxPhotoBytes)
        {
            return false;
        }

        ReadOnlySpan<byte> png = [137, 80, 78, 71, 13, 10, 26, 10];
        return image.AsSpan().StartsWith(png) || image is [255, 216, 255, ..];
    }
}
