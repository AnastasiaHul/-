using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class MapRouteService(
    IRepository<Place> places,
    TripService trips,
    ItineraryService itinerary,
    ServiceOperations operations)
{
    public Task<string> BuildUrlAsync(
        int tripId,
        DateTime day,
        string travelMode)
    {
        return operations.RunAsync(
            "BuildTripRoute",
            async () =>
            {
                // Перевіряємо власника подорожі.
                Trip trip = await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                if (day == default
                    || day.Date < trip.StartDate.Date
                    || day.Date > trip.EndDate.Date)
                {
                    throw new ArgumentException(
                        "Виберіть день у межах подорожі.");
                }

                if (travelMode != "walking"
                    && travelMode != "driving")
                {
                    throw new ArgumentException(
                        "Виберіть спосіб пересування.");
                }

                // Отримуємо готовий план подорожі.
                IReadOnlyList<ItineraryVisit> allVisits =
                    await itinerary.ListAsync(tripId)
                        .ConfigureAwait(false);

                // Залишаємо місця вибраного дня.
                List<ItineraryVisit> visits = allVisits
                    .Where(visit =>
                        visit.VisitAt.Date == day.Date)
                    .OrderBy(visit => visit.VisitAt)
                    .ToList();

                if (visits.Count < 2)
                {
                    throw new ArgumentException(
                        "Для маршруту додайте до плану "
                        + "щонайменше два місця на цей день.");
                }

                if (visits.Count > 11)
                {
                    throw new ArgumentException(
                        "Ця версія підтримує до 11 місць "
                        + "в одному маршруті за день.");
                }

                var addresses = new List<string>();

                foreach (ItineraryVisit visit in visits)
                {
                    Place place =
                        await places.FindAsync(visit.PlaceId)
                            .ConfigureAwait(false)
                        ?? throw new ArgumentException(
                            $"Місце «{visit.PlaceName}» "
                            + "не знайдено.");

                    if (string.IsNullOrWhiteSpace(
                        place.Address))
                    {
                        throw new ArgumentException(
                            $"Для місця «{place.Name}» "
                            + "партнер має вказати адресу.");
                    }

                    // Адреса має містити місто,
                    // вулицю та номер будинку.
                    addresses.Add(place.Address.Trim());
                }

                // Перше місце — початок маршруту.
                string origin = Uri.EscapeDataString(
                    addresses[0]);

                // Останнє місце — кінець маршруту.
                string destination = Uri.EscapeDataString(
                    addresses[^1]);

                string url =
                    "https://www.google.com/maps/dir/"
                    + "?api=1"
                    + $"&origin={origin}"
                    + $"&destination={destination}"
                    + $"&travelmode={travelMode}";

                // Решта місць — проміжні зупинки.
                if (addresses.Count > 2)
                {
                    string intermediate = string.Join(
                        "|",
                        addresses
                            .Skip(1)
                            .Take(addresses.Count - 2));

                    url += "&waypoints="
                        + Uri.EscapeDataString(intermediate);
                }

                if (url.Length > 2048)
                {
                    throw new ArgumentException(
                        "Посилання на маршрут занадто довге. "
                        + "Зменште кількість місць на цей день "
                        + "або скоротіть їхні адреси.");
                }

                return url;
            });
    }
}