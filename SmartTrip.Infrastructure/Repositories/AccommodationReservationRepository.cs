using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class AccommodationReservationRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : IAccommodationReservationRepository
{
    public async Task<IReadOnlyList<AccommodationReservation>>
        ListForTripAsync(int tripId)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        return await context
            .Set<AccommodationReservation>()
            .AsNoTracking()
            .Where(x => x.TripId == tripId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<int>> ListUnavailableIdsAsync(
        DateTime checkIn,
        DateTime checkOut)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        return await context
            .Set<AccommodationReservation>()
            .AsNoTracking()
            .Where(x =>
                x.Status == "Confirmed"
                && x.CheckIn < checkOut
                && x.CheckOut > checkIn)
            .Select(x => x.AccommodationId)
            .Distinct()
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<bool> TryReserveAsync(
        AccommodationReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        await using var transaction =
            await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable)
            .ConfigureAwait(false);

        bool unavailable = await context
            .Set<AccommodationReservation>()
            .AnyAsync(x =>
                x.AccommodationId == reservation.AccommodationId
                && x.Status == "Confirmed"
                && x.CheckIn < reservation.CheckOut
                && x.CheckOut > reservation.CheckIn)
            .ConfigureAwait(false);

        if (unavailable)
        {
            return false;
        }

        context.Set<AccommodationReservation>().Add(reservation);

        await context.SaveChangesAsync()
            .ConfigureAwait(false);

        await transaction.CommitAsync()
            .ConfigureAwait(false);

        return true;
    }
}