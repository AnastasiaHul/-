using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class ExcursionReservationRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : IExcursionReservationRepository
{
    public async Task<IReadOnlyList<ExcursionReservation>>
        ListForTripAsync(int tripId)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        return await context
            .Set<ExcursionReservation>()
            .AsNoTracking()
            .Where(x => x.TripId == tripId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<int, long>>
        GetReservedCountsAsync(
            IReadOnlyList<int> sessionIds)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);

        int[] ids = sessionIds
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<int, long>();
        }

        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        return await context
            .Set<ExcursionReservation>()
            .AsNoTracking()
            .Where(x =>
                ids.Contains(x.ExcursionSessionId)
                && x.Status == "Confirmed")
            .GroupBy(x => x.ExcursionSessionId)
            .Select(group => new
            {
                SessionId = group.Key,
                Reserved = group.Sum(
                    x => (long)x.ParticipantsCount)
            })
            .ToDictionaryAsync(
                x => x.SessionId,
                x => x.Reserved)
            .ConfigureAwait(false);
    }

    public async Task<bool> TryReserveAsync(
        ExcursionReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        if (reservation.ParticipantsCount < 1)
        {
            throw new ArgumentException(
                "Кількість учасників має бути більшою за нуль.",
                nameof(reservation));
        }

        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        await using var transaction =
            await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable)
            .ConfigureAwait(false);

        ExcursionSession? excursion = await context
            .Set<ExcursionSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ExcursionSessionId
                    == reservation.ExcursionSessionId
                && x.IsActive)
            .ConfigureAwait(false);

        if (excursion is null)
        {
            return false;
        }

        long reserved = await context
            .Set<ExcursionReservation>()
            .Where(x =>
                x.ExcursionSessionId
                    == reservation.ExcursionSessionId
                && x.Status == "Confirmed")
            .SumAsync(x => (long)x.ParticipantsCount)
            .ConfigureAwait(false);

        if (reserved + reservation.ParticipantsCount
            > excursion.Capacity)
        {
            return false;
        }

        context.Set<ExcursionReservation>().Add(reservation);

        await context.SaveChangesAsync()
            .ConfigureAwait(false);

        await transaction.CommitAsync()
            .ConfigureAwait(false);

        return true;
    }
}