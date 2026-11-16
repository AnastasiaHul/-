using Microsoft.EntityFrameworkCore;
using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class AdministrationRepository(IDbContextFactory<ApplicationDbContext> factory)
    : IAdministrationRepository
{
    public async Task<IReadOnlyList<PendingPlace>> ListPendingAsync()
    {
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        return await db.Places.AsNoTracking().Where(x => x.Status == "OnReview" && x.PartnerId != null)
            .OrderByDescending(x => x.PlaceId).Take(100)
            .Select(x => new PendingPlace(x.PlaceId, x.Name, x.Address, x.Description, x.Category, x.PartnerId))
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<AccountSummary?> FindAccountAsync(string email)
    {
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        return await db.Users.AsNoTracking().Where(x => x.Email == email)
            .Select(x => new AccountSummary(x.UserId, x.Email, x.Role, x.IsBlocked))
            .SingleOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<ExcursionSummary?> FindExcursionAsync(string code)
    {
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        return await db.ExcursionReservations.AsNoTracking().Where(x => x.BookingCode.ToUpper() == code)
            .Select(x => new ExcursionSummary(x.ExcursionReservationId, x.BookingCode, x.TripId, x.Status, x.CancellationReason))
            .SingleOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<Result> ReviewPlaceAsync(int placeId, bool approve, string reason, int actor)
    {
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        var place = await db.Places.FindAsync(placeId).ConfigureAwait(false);
        if (place is null || place.PartnerId is null || place.Status != "OnReview")
        {
            return new Failure("Місце вже не очікує перевірки. Оновіть список.");
        }

        place.EditRevision++;
        place.Status = approve ? "Published" : "NeedsChanges";
        place.ModerationReason = reason;
        return await SaveAsync(db, approve ? AdministrationService.ApprovePlace : AdministrationService.ReturnPlace,
            placeId, reason, actor).ConfigureAwait(false);
    }

    public async Task<Result> SetAccountBlockedAsync(int userId, bool block, string reason, int actor)
    {
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        var user = await db.Users.FindAsync(userId).ConfigureAwait(false);
        if (user is null || userId == actor || user.Role is not ("Tourist" or "Partner"))
        {
            return new Failure("Можна змінювати доступ лише туристів і партнерів.");
        }

        if (user.IsBlocked == block)
        {
            return new Failure("Акаунт уже має цей статус.");
        }

        user.IsBlocked = block;
        return await SaveAsync(db, block ? AdministrationService.BlockAccount : AdministrationService.UnblockAccount,
            userId, reason, actor).ConfigureAwait(false);
    }

    public async Task<Result> CancelExcursionAsync(int reservationId, string reason, int actor)
    {
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        var reservation = await db.ExcursionReservations.FindAsync(reservationId).ConfigureAwait(false);
        if (reservation is null || reservation.Status != "Confirmed")
        {
            return new Failure("Можна скасувати лише підтверджене бронювання.");
        }

        reservation.Status = "Cancelled";
        reservation.CancellationReason = reason;
        return await SaveAsync(db, AdministrationService.CancelExcursion,
            reservationId, reason, actor).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AdminAuditEntry>> ListAuditAsync(DateTime? fromUtc, DateTime? untilUtc, string? action)
    {
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        return await db.Set<AdminAuditEntry>().AsNoTracking()
            .Where(x => (fromUtc == null || x.OccurredAtUtc >= fromUtc)
                && (untilUtc == null || x.OccurredAtUtc < untilUtc)
                && (action == null || x.Action == action))
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.AdminAuditEntryId)
            .Take(200).ToListAsync().ConfigureAwait(false);
    }

    private static async Task<Result> SaveAsync(ApplicationDbContext db,
        string action, int target, string reason, int actor)
    {
        db.Set<AdminAuditEntry>().Add(new AdminAuditEntry
        {
            ActorId = actor, Action = action, TargetId = target, Reason = reason,
            OccurredAtUtc = DateTime.UtcNow
        });

        // EF saves the change and audit entry together in one transaction.
        await db.SaveChangesAsync().ConfigureAwait(false);
        return Result.Success();
    }
}
