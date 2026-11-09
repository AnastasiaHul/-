using Microsoft.EntityFrameworkCore;

namespace SmartTrip.Infrastructure.Data;

public static class PhotoDatabaseInitializer
{
    public static async Task InitializeSqliteAsync(IDbContextFactory<ApplicationDbContext> factory)
    {
        await using ApplicationDbContext context = await factory.CreateDbContextAsync()
            .ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "TripPhotos" (
                "TripPhotoId" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "TripId" INTEGER NOT NULL REFERENCES "Trips" ("TripId") ON DELETE CASCADE,
                "PlaceId" INTEGER NULL REFERENCES "Places" ("PlaceId") ON DELETE SET NULL,
                "Caption" TEXT NOT NULL,
                "PlaceName" TEXT NOT NULL,
                "Address" TEXT NOT NULL,
                "Image" BLOB NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_TripPhotos_TripId" ON "TripPhotos" ("TripId");
            CREATE INDEX IF NOT EXISTS "IX_TripPhotos_PlaceId" ON "TripPhotos" ("PlaceId");
            """).ConfigureAwait(false);
    }
}
