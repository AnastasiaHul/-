using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace SmartTrip.Application.Services;
public sealed class PackingService(IRepository<PackingItem> items, TripService trips, ServiceOperations operations)
{
    public Task<Result<IReadOnlyList<PackingItem>>> ListAsync(int tripId)
    {
        return operations.RunResultAsync<IReadOnlyList<PackingItem>>("ListPackingItems", async () =>
        {
            var check1 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check1.IsFailure)
            {
                return new Failure(check1.Error);
            }

            return Result<IReadOnlyList<PackingItem>>.Success(await items.ListAsync(x => x.TripId == tripId).ConfigureAwait(false));
        });
    }

    public Task<Result> AddAsync(int tripId, string name)
    {
        return operations.RunResultAsync("AddPackingItem", async () =>
        {
            var check2 = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (check2.IsFailure)
            {
                return new Failure(check2.Error);
            }

            var check3 = ServiceOperations.TryRequired(name, "Назва речі", 150);
            if (check3.IsFailure)
            {
                return new Failure(check3.Error);
            }

            string validName = check3.Value!;
            IReadOnlyList<PackingItem> existing = await items.ListAsync(x => x.TripId == tripId).ConfigureAwait(false);
            if (existing.Any(x => string.Equals(x.Name, validName, StringComparison.OrdinalIgnoreCase)))
            {
                return new Failure("Така річ уже є у списку.");
            }

            await items.AddAsync(new PackingItem { TripId = tripId, Name = validName }).ConfigureAwait(false);
            return Result.Success();
        });
    }

    public Task<Result> ToggleAsync(int id)
    {
        return operations.RunResultAsync("TogglePackingItem", async () =>
        {
            var check4 = await OwnedAsync(id).ConfigureAwait(false);
            if (check4.IsFailure)
            {
                return new Failure(check4.Error);
            }

            PackingItem item = check4.Value!;
            item.IsPacked = !item.IsPacked;
            await items.UpdateAsync(item).ConfigureAwait(false);
            return Result.Success();
        });
    }

    public Task<Result> DeleteAsync(int id)
    {
        return operations.RunResultAsync("DeletePackingItem", async () =>
        {
            var check5 = await OwnedAsync(id).ConfigureAwait(false);
            if (check5.IsFailure)
            {
                return new Failure(check5.Error);
            }

            PackingItem item = check5.Value!;
            await items.DeleteAsync(item).ConfigureAwait(false);
            return Result.Success();
        });
    }

    private async Task<Result<PackingItem>> OwnedAsync(int id)
    {
        PackingItem? item = await items.FindAsync(id).ConfigureAwait(false);
        if (item is null)
        {
            return new Failure("Річ не знайдена.");
        }

        var check6 = await trips.OwnedAsync(item.TripId).ConfigureAwait(false);
        if (check6.IsFailure)
        {
            return new Failure(check6.Error);
        }

        return Result<PackingItem>.Success(item);
    }
}
