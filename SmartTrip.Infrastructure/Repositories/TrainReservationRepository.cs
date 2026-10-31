using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class TrainReservationRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : ITrainReservationRepository
{
    public async Task<IReadOnlyList<TrainReservation>>
        ListForTripAsync(int tripId)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        return await context.Set<TrainReservation>()
            .AsNoTracking()
            .Where(reservation => reservation.TripId == tripId)
            .OrderByDescending(reservation => reservation.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<int>> ListReservedSeatsAsync(
        int trainDepartureId,
        int wagonNumber)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        return await context.Set<TrainReservation>()
            .AsNoTracking()
            .Where(reservation =>
                reservation.TrainDepartureId == trainDepartureId
                && reservation.WagonNumber == wagonNumber
                && reservation.Status == "Confirmed")
            .OrderBy(reservation => reservation.SeatNumber)
            .Select(reservation => reservation.SeatNumber)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<bool> TryReserveAsync(
        TrainReservation reservation)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        context.Set<TrainReservation>().Add(reservation);

        try
        {
            await context.SaveChangesAsync()
                .ConfigureAwait(false);

            return true;
        }
        catch (DbUpdateException exception)
            when (IsSeatConflict(exception))
        {
            return false;
        }
    }

    private static bool IsSeatConflict(
        DbUpdateException exception)
    {
        return exception.InnerException switch
        {
            SqliteException sqlite =>
                sqlite.SqliteExtendedErrorCode == 2067
                && sqlite.Message.Contains(
                    "TrainReservations.TrainDepartureId",
                    StringComparison.Ordinal)
                && sqlite.Message.Contains(
                    "TrainReservations.WagonNumber",
                    StringComparison.Ordinal)
                && sqlite.Message.Contains(
                    "TrainReservations.SeatNumber",
                    StringComparison.Ordinal),

            PostgresException postgres =>
                postgres.SqlState == "23505"
                && postgres.ConstraintName
                    == "UX_TrainReservation_Seat",

            _ => false
        };
    }
}