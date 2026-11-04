using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace SmartTrip.Application.Services;
public sealed class MapRouteService(IRepository<Place> places, TripService trips, ItineraryService itinerary, ServiceOperations operations)
{
    public Task<Result<string>> BuildUrlAsync(int tripId, DateTime day, string travelMode)
    {
        return operations.RunResultAsync<string>("BuildTripRoute", async () =>
        {
            var check1 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check1.IsFailure)
            {
                return new Failure(check1.Error);
            }

            // Перевіряємо власника подорожі.
            Trip trip = check1.Value!;
            if (day == default || day.Date < trip.StartDate.Date || day.Date > trip.EndDate.Date)
            {
                return new Failure("Виберіть день у межах подорожі.");
            }

            if (travelMode != "walking" && travelMode != "driving")
            {
                return new Failure("Виберіть спосіб пересування.");
            }

            var check2 = await itinerary.ListAsync(tripId).ConfigureAwait(false);
            if (check2.IsFailure)
            {
                return new Failure(check2.Error);
            }

            // Отримуємо готовий план подорожі.
            IReadOnlyList<ItineraryVisit> allVisits = check2.Value!;
            // Залишаємо місця вибраного дня.
            List<ItineraryVisit> visits = allVisits.Where(visit => visit.VisitAt.Date == day.Date).OrderBy(visit => visit.VisitAt).ToList();
            if (visits.Count < 2)
            {
                return new Failure("Для маршруту додайте до плану " + "щонайменше два місця на цей день.");
            }

            if (visits.Count > 11)
            {
                return new Failure("Ця версія підтримує до 11 місць " + "в одному маршруті за день.");
            }

            var addresses = new List<string>();
            foreach (ItineraryVisit visit in visits)
            {
                Place? place = await places.FindAsync(visit.PlaceId).ConfigureAwait(false);
                if (place is null)
                {
                    return new Failure($"Місце «{visit.PlaceName}» " + "не знайдено.");
                }

                if (string.IsNullOrWhiteSpace(place.Address))
                {
                    return new Failure($"Для місця «{place.Name}» " + "партнер має вказати адресу.");
                }

                // Адреса має містити місто,
                // вулицю та номер будинку.
                addresses.Add(place.Address.Trim());
            }

            // Перше місце — початок маршруту.
            string origin = Uri.EscapeDataString(addresses[0]);
            // Останнє місце — кінець маршруту.
            string destination = Uri.EscapeDataString(addresses[^1]);
            string url = "https://www.google.com/maps/dir/" + "?api=1" + $"&origin={origin}" + $"&destination={destination}" + $"&travelmode={travelMode}";
            // Решта місць — проміжні зупинки.
            if (addresses.Count > 2)
            {
                string intermediate = string.Join("|", addresses.Skip(1).Take(addresses.Count - 2));
                url += "&waypoints=" + Uri.EscapeDataString(intermediate);
            }

            if (url.Length > 2048)
            {
                return new Failure("Посилання на маршрут занадто довге. " + "Зменште кількість місць на цей день " + "або скоротіть їхні адреси.");
            }

            return Result<string>.Success(url);
        });
    }
}
