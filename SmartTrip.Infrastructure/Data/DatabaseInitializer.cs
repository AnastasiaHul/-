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

            const string sql = """
                CREATE TABLE IF NOT EXISTS "ItineraryVisits" (
                    "ItineraryVisitId" INTEGER NOT NULL
                        CONSTRAINT "PK_ItineraryVisits"
                        PRIMARY KEY AUTOINCREMENT,
                    "TripId" INTEGER NOT NULL,
                    "PlaceId" INTEGER NOT NULL,
                    "PlaceName" TEXT NOT NULL,
                    "VisitAt" TEXT NOT NULL,
                    "Notes" TEXT NOT NULL,
                    FOREIGN KEY ("TripId")
                        REFERENCES "Trips" ("TripId")
                        ON DELETE CASCADE,
                    FOREIGN KEY ("PlaceId")
                        REFERENCES "Places" ("PlaceId")
                        ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX IF NOT EXISTS
                    "IX_ItineraryVisits_TripId_VisitAt"
                    ON "ItineraryVisits" ("TripId", "VisitAt");

                CREATE INDEX IF NOT EXISTS
                    "IX_ItineraryVisits_PlaceId"
                    ON "ItineraryVisits" ("PlaceId");
                """;

            await using (var transaction =
                await context.Database.BeginTransactionAsync()
                    .ConfigureAwait(false))
            {
                await context.Database.ExecuteSqlRawAsync(sql)
                    .ConfigureAwait(false);

                await transaction.CommitAsync()
                    .ConfigureAwait(false);
            }

            await TrainDatabaseInitializer.InitializeSqliteAsync(
                context)
                .ConfigureAwait(false);

            await AccommodationDatabaseInitializer
                .InitializeSqliteAsync(context)
                .ConfigureAwait(false);

            await CarDatabaseInitializer.InitializeSqliteAsync(
                context)
                .ConfigureAwait(false);

            await ExcursionDatabaseInitializer
                .InitializeSqliteAsync(context)
                .ConfigureAwait(false);
        }
        else
        {
            await context.Database.MigrateAsync()
                .ConfigureAwait(false);
        }
    }
}