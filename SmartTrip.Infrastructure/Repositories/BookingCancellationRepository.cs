using Microsoft.EntityFrameworkCore;
using SmartTrip.Application.Contracts;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class BookingCancellationRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : IBookingCancellationRepository
{
    private const string Confirmed = "Confirmed";
    private const string Cancelled = "Cancelled";

    public async Task<bool> TryCancelAsync(
        BookingKind kind,
        int reservationId,
        int tripId,
        string expenseMarker)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync().ConfigureAwait(false);
        await using var transaction =
            await context.Database.BeginTransactionAsync().ConfigureAwait(false);

        int changed = kind switch
        {
            BookingKind.Train => await context.TrainReservations
                .Where(x => x.TrainReservationId == reservationId
                    && x.TripId == tripId && x.Status == Confirmed)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, Cancelled))
                .ConfigureAwait(false),
            BookingKind.Housing => await context.AccommodationReservations
                .Where(x => x.AccommodationReservationId == reservationId
                    && x.TripId == tripId && x.Status == Confirmed)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, Cancelled))
                .ConfigureAwait(false),
            BookingKind.Car => await context.CarReservations
                .Where(x => x.CarReservationId == reservationId
                    && x.TripId == tripId && x.Status == Confirmed)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, Cancelled))
                .ConfigureAwait(false),
            BookingKind.Excursion => await context.ExcursionReservations
                .Where(x => x.ExcursionReservationId == reservationId
                    && x.TripId == tripId && x.Status == Confirmed)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, Cancelled)
                    .SetProperty(x => x.CancellationReason, "Скасовано туристом"))
                .ConfigureAwait(false),
            _ => 0
        };

        if (changed != 1)
        {
            return false;
        }

        await context.Expenses
            .Where(x => x.TripId == tripId && x.Description.EndsWith(expenseMarker))
            .ExecuteDeleteAsync()
            .ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        return true;
    }
}
