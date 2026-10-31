using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Infrastructure.Data;

public static class ExcursionDatabaseInitializer
{
    private const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS "ExcursionSessions" (
            "ExcursionSessionId" INTEGER NOT NULL
                CONSTRAINT "PK_ExcursionSessions"
                PRIMARY KEY AUTOINCREMENT,
            "Title" TEXT NOT NULL,
            "City" TEXT NOT NULL,
            "MeetingAddress" TEXT NOT NULL,
            "Description" TEXT NOT NULL,
            "StartsAt" TEXT NOT NULL,
            "DurationMinutes" INTEGER NOT NULL,
            "Capacity" INTEGER NOT NULL,
            "PricePerPerson" TEXT NOT NULL,
            "IsActive" INTEGER NOT NULL,
            CONSTRAINT "CK_ExcursionSession_Settings"
                CHECK ("Capacity" > 0 AND "DurationMinutes" > 0)
        );

        CREATE INDEX IF NOT EXISTS
            "IX_ExcursionSessions_City_StartsAt_IsActive"
            ON "ExcursionSessions" ("City", "StartsAt", "IsActive");

        CREATE TABLE IF NOT EXISTS "ExcursionReservations" (
            "ExcursionReservationId" INTEGER NOT NULL
                CONSTRAINT "PK_ExcursionReservations"
                PRIMARY KEY AUTOINCREMENT,
            "TripId" INTEGER NOT NULL,
            "ExcursionSessionId" INTEGER NOT NULL,
            "ParticipantsCount" INTEGER NOT NULL,
            "GuestName" TEXT NOT NULL,
            "BookingCode" TEXT NOT NULL,
            "TotalPrice" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            CONSTRAINT "CK_ExcursionReservation_Participants"
                CHECK ("ParticipantsCount" > 0),
            CONSTRAINT "FK_ExcursionReservations_Trips_TripId"
                FOREIGN KEY ("TripId")
                REFERENCES "Trips" ("TripId")
                ON DELETE CASCADE,
            CONSTRAINT
                "FK_ExcursionReservations_ExcursionSessions_ExcursionSessionId"
                FOREIGN KEY ("ExcursionSessionId")
                REFERENCES "ExcursionSessions" ("ExcursionSessionId")
                ON DELETE RESTRICT
        );

        CREATE INDEX IF NOT EXISTS
            "IX_ExcursionReservations_TripId"
            ON "ExcursionReservations" ("TripId");

        CREATE UNIQUE INDEX IF NOT EXISTS
            "IX_ExcursionReservations_BookingCode"
            ON "ExcursionReservations" ("BookingCode");

        CREATE INDEX IF NOT EXISTS
            "IX_ExcursionReservations_ExcursionSessionId_Status"
            ON "ExcursionReservations" ("ExcursionSessionId", "Status");
        """;

    public static async Task InitializeSqliteAsync(
        ApplicationDbContext context)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync()
                .ConfigureAwait(false);

        await context.Database.ExecuteSqlRawAsync(CreateTablesSql)
            .ConfigureAwait(false);

        await SeedExcursionsAsync(context)
            .ConfigureAwait(false);

        await transaction.CommitAsync()
            .ConfigureAwait(false);
    }

    private static async Task SeedExcursionsAsync(
        ApplicationDbContext context)
    {
        DateTime firstDay = DateTime.SpecifyKind(
            DateTime.Today,
            DateTimeKind.Utc);

        DateTime lastDay = firstDay.AddDays(60);

        var saved = await context.ExcursionSessions
            .AsNoTracking()
            .Where(x =>
                x.StartsAt >= firstDay
                && x.StartsAt < lastDay)
            .Select(x => new
            {
                x.Title,
                x.City,
                x.StartsAt
            })
            .ToListAsync()
            .ConfigureAwait(false);

        var existing = saved
            .Select(x => (x.Title, x.City, x.StartsAt))
            .ToHashSet();

        DemoExcursion[] examples =
        [
            new DemoExcursion(
                "Історичний центр Львова",
                "Львів",
                "Львів, площа Ринок, 1",
                "Навчальна пішохідна екскурсія центральними вулицями.",
                10,
                120,
                12,
                350m),
            new DemoExcursion(
                "Львівські дворики",
                "Львів",
                "Львів, вул. Вірменська, 7",
                "Навчальний маршрут затишними двориками старого міста.",
                15,
                90,
                10,
                300m),
            new DemoExcursion(
                "Прогулянка старим Києвом",
                "Київ",
                "Київ, Володимирська вулиця, 24",
                "Навчальна екскурсія історичними місцями столиці.",
                10,
                120,
                15,
                450m),
            new DemoExcursion(
                "Вечірній Київ",
                "Київ",
                "Київ, Хрещатик, 1",
                "Навчальна прогулянка центральними вулицями міста.",
                17,
                90,
                12,
                400m),
            new DemoExcursion(
                "Знайомство з Одесою",
                "Одеса",
                "Одеса, вул. Дерибасівська, 12",
                "Навчальна екскурсія історичним центром Одеси.",
                10,
                120,
                14,
                400m),
            new DemoExcursion(
                "Морська прогулянка містом",
                "Одеса",
                "Одеса, Приморський бульвар, 1",
                "Навчальний пішохідний маршрут із морськими краєвидами.",
                16,
                90,
                10,
                350m)
        ];

        for (int day = 0; day < 60; day++)
        {
            foreach (DemoExcursion example in examples)
            {
                DateTime startsAt = firstDay
                    .AddDays(day)
                    .AddHours(example.StartHour);

                if (!existing.Add((
                    example.Title,
                    example.City,
                    startsAt)))
                {
                    continue;
                }

                context.ExcursionSessions.Add(
                    new ExcursionSession
                    {
                        Title = example.Title,
                        City = example.City,
                        MeetingAddress = example.MeetingAddress,
                        Description = example.Description,
                        StartsAt = startsAt,
                        DurationMinutes = example.DurationMinutes,
                        Capacity = example.Capacity,
                        PricePerPerson = example.PricePerPerson,
                        IsActive = true
                    });
            }
        }

        await context.SaveChangesAsync()
            .ConfigureAwait(false);
    }

    private sealed record DemoExcursion(
        string Title,
        string City,
        string MeetingAddress,
        string Description,
        int StartHour,
        int DurationMinutes,
        int Capacity,
        decimal PricePerPerson);
}