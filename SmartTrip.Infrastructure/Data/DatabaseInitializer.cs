using Microsoft.EntityFrameworkCore;

namespace SmartTrip.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IDbContextFactory<ApplicationDbContext> factory)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        if (context.Database.IsSqlite())
        {
            await context.Database.EnsureCreatedAsync()
                .ConfigureAwait(false);
        }
        else
        {
            await context.Database.MigrateAsync()
                .ConfigureAwait(false);
        }
    }
}