using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class CarReservationRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : ICarReservationRepository
{
    public async Task<IReadOnlyList<CarReservation>>
        ListForTripAsync(int tripId)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        return await context
            .Set<CarReservation>()
            .AsNoTracking()
            .Where(x => x.TripId == tripId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<int>> ListUnavailableIdsAsync(
        DateTime pickupDate,
        DateTime returnDate)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        return await context
            .Set<CarReservation>()
            .AsNoTracking()
            .Where(x =>
                x.Status == "Confirmed"
                && x.PickupDate < returnDate
                && x.ReturnDate > pickupDate)
            .Select(x => x.RentalCarId)
            .Distinct()
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<bool> TryReserveAsync(
        CarReservation reservation)
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
            .Set<CarReservation>()
            .AnyAsync(x =>
                x.RentalCarId == reservation.RentalCarId
                && x.Status == "Confirmed"
                && x.PickupDate < reservation.ReturnDate
                && x.ReturnDate > reservation.PickupDate)
            .ConfigureAwait(false);

        if (unavailable)
        {
            return false;
        }

        context.Set<CarReservation>().Add(reservation);

        await context.SaveChangesAsync()
            .ConfigureAwait(false);

        await transaction.CommitAsync()
            .ConfigureAwait(false);

        return true;
    }
}