using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class PlaceRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : EfRepository<Place>(factory)
{
    public override async Task UpdateAsync(Place entity)
    {
        await using var context = await Factory.CreateDbContextAsync().ConfigureAwait(false);
        context.Places.Update(entity);
        entity.EditRevision++;
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}