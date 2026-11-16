using SmartTrip.Application.Common;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Contracts;

public sealed record PendingPlace(int PlaceId, string Name, string? Address, string? Description, string Category, int? PartnerId);
public sealed record AccountSummary(int UserId, string Email, string Role, bool IsBlocked);
public sealed record ExcursionSummary(int ReservationId, string BookingCode, int TripId, string Status, string? CancellationReason);

public interface IAdministrationRepository
{
    Task<IReadOnlyList<PendingPlace>> ListPendingAsync();
    Task<AccountSummary?> FindAccountAsync(string email);
    Task<ExcursionSummary?> FindExcursionAsync(string code);
    Task<Result> ReviewPlaceAsync(int placeId, bool approve, string reason, int actor);
    Task<Result> SetAccountBlockedAsync(int userId, bool block, string reason, int actor);
    Task<Result> CancelExcursionAsync(int reservationId, string reason, int actor);
    Task<IReadOnlyList<AdminAuditEntry>> ListAuditAsync(DateTime? fromUtc, DateTime? untilUtc, string? action);
}
