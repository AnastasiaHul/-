using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Contracts;

public interface ITripCopyRepository
{
    // Зберігає нову подорож разом із планом і речами однією транзакцією.
    Task AddAsync(
        Trip trip,
        IReadOnlyList<ItineraryVisit> visits,
        IReadOnlyList<PackingItem> items);
}
