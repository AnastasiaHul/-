using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class PackingService(
    IRepository<PackingItem> items,
    TripService trips,
    ServiceOperations operations)
{
    public Task<IReadOnlyList<PackingItem>> ListAsync(
        int tripId)
    {
        return operations.RunAsync(
            "ListPackingItems",
            async () =>
            {
                await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                return await items.ListAsync(
                    x => x.TripId == tripId)
                    .ConfigureAwait(false);
            });
    }

    public Task AddAsync(int tripId, string name)
    {
        return operations.RunAsync(
            "AddPackingItem",
            async () =>
            {
                await trips.OwnedAsync(tripId)
                    .ConfigureAwait(false);

                string validName =
                    ServiceOperations.Required(
                        name, "Назва речі", 150);

                IReadOnlyList<PackingItem> existing =
                    await items.ListAsync(
                        x => x.TripId == tripId)
                    .ConfigureAwait(false);

                if (existing.Any(x => string.Equals(
                    x.Name,
                    validName,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException(
                        "Така річ уже є у списку.");
                }

                await items.AddAsync(new PackingItem
                {
                    TripId = tripId,
                    Name = validName
                }).ConfigureAwait(false);
            });
    }

    public Task ToggleAsync(int id)
    {
        return operations.RunAsync(
            "TogglePackingItem",
            async () =>
            {
                PackingItem item = await OwnedAsync(id)
                    .ConfigureAwait(false);

                item.IsPacked = !item.IsPacked;

                await items.UpdateAsync(item)
                    .ConfigureAwait(false);
            });
    }

    public Task DeleteAsync(int id)
    {
        return operations.RunAsync(
            "DeletePackingItem",
            async () =>
            {
                PackingItem item = await OwnedAsync(id)
                    .ConfigureAwait(false);

                await items.DeleteAsync(item)
                    .ConfigureAwait(false);
            });
    }

    private async Task<PackingItem> OwnedAsync(int id)
    {
        PackingItem item = await items.FindAsync(id)
            .ConfigureAwait(false)
            ?? throw new ArgumentException(
                "Річ не знайдена.");

        await trips.OwnedAsync(item.TripId)
            .ConfigureAwait(false);

        return item;
    }
}