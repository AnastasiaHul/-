using Microsoft.EntityFrameworkCore;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class TripCopyRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : ITripCopyRepository
{
    public async Task AddAsync(
        Trip trip,
        IReadOnlyList<ItineraryVisit> visits,
        IReadOnlyList<PackingItem> items)
    {
        ArgumentNullException.ThrowIfNull(trip);
        ArgumentNullException.ThrowIfNull(visits);
        ArgumentNullException.ThrowIfNull(items);

        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync().ConfigureAwait(false);
        await using var transaction =
            await context.Database.BeginTransactionAsync().ConfigureAwait(false);

        context.Trips.Add(trip);
        await context.SaveChangesAsync().ConfigureAwait(false);

        foreach (ItineraryVisit visit in visits)
        {
            visit.TripId = trip.TripId;
        }

        foreach (PackingItem item in items)
        {
            item.TripId = trip.TripId;
        }

        context.ItineraryVisits.AddRange(visits);
        context.PackingItems.AddRange(items);
        await context.SaveChangesAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
    }
}
