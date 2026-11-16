using Microsoft.EntityFrameworkCore;

namespace SmartTrip.Infrastructure.Data;

public static class AdministrationDatabaseInitializer
{
    public static async Task InitializeSqliteAsync(IDbContextFactory<ApplicationDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync().ConfigureAwait(false);
        if (!await HasColumnAsync(db, """PRAGMA table_info("Users");""", "IsBlocked").ConfigureAwait(false))
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Users" ADD COLUMN "IsBlocked" INTEGER NOT NULL DEFAULT 0;""").ConfigureAwait(false);
        }

        if (!await HasColumnAsync(db, """PRAGMA table_info("Places");""", "ModerationReason").ConfigureAwait(false))
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Places" ADD COLUMN "ModerationReason" TEXT NULL;""").ConfigureAwait(false);
        }

        if (!await HasColumnAsync(db, """PRAGMA table_info("Places");""", "EditRevision").ConfigureAwait(false))
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Places" ADD COLUMN "EditRevision" INTEGER NOT NULL DEFAULT 0;""").ConfigureAwait(false);
        }

        if (!await HasColumnAsync(db, """PRAGMA table_info("ExcursionReservations");""", "CancellationReason")
            .ConfigureAwait(false))
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "ExcursionReservations" ADD COLUMN "CancellationReason" TEXT NULL;""").ConfigureAwait(false);
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AdminAuditEntries" (
                "AdminAuditEntryId" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "ActorId" INTEGER NOT NULL,
                "Action" TEXT NOT NULL,
                "TargetId" INTEGER NOT NULL,
                "Reason" TEXT NOT NULL,
                "OccurredAtUtc" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_AdminAuditEntries_OccurredAtUtc"
                ON "AdminAuditEntries" ("OccurredAtUtc");
            """).ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
    }

    private static async Task<bool> HasColumnAsync(ApplicationDbContext db, string sql, string column)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions
            .GetDbTransaction(db.Database.CurrentTransaction!);
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (reader.GetString(1) == column)
            {
                return true;
            }
        }

        return false;
    }
}
