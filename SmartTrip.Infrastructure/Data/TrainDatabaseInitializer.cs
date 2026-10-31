using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Infrastructure.Data;

public static class TrainDatabaseInitializer
{
    private const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS "TrainDepartures" (
            "TrainDepartureId" INTEGER NOT NULL
                CONSTRAINT "PK_TrainDepartures"
                PRIMARY KEY AUTOINCREMENT,
            "TrainNumber" TEXT NOT NULL,
            "DepartureCity" TEXT NOT NULL,
            "ArrivalCity" TEXT NOT NULL,
            "DepartureAt" TEXT NOT NULL,
            "ArrivalAt" TEXT NOT NULL,
            "WagonCount" INTEGER NOT NULL,
            "SeatsPerWagon" INTEGER NOT NULL,
            "TicketPrice" TEXT NOT NULL,
            CONSTRAINT "CK_TrainDeparture_Capacity"
                CHECK ("WagonCount" > 0 AND "SeatsPerWagon" > 0)
        );

        CREATE INDEX IF NOT EXISTS
            "IX_TrainDepartures_DepartureCity_ArrivalCity_DepartureAt"
            ON "TrainDepartures"
            ("DepartureCity", "ArrivalCity", "DepartureAt");

        CREATE TABLE IF NOT EXISTS "TrainReservations" (
            "TrainReservationId" INTEGER NOT NULL
                CONSTRAINT "PK_TrainReservations"
                PRIMARY KEY AUTOINCREMENT,
            "TripId" INTEGER NOT NULL,
            "TrainDepartureId" INTEGER NOT NULL,
            "WagonNumber" INTEGER NOT NULL,
            "SeatNumber" INTEGER NOT NULL,
            "PassengerName" TEXT NOT NULL,
            "BookingCode" TEXT NOT NULL,
            "TotalPrice" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            CONSTRAINT "CK_TrainReservation_SeatNumbers"
                CHECK ("WagonNumber" > 0 AND "SeatNumber" > 0),
            FOREIGN KEY ("TripId")
                REFERENCES "Trips" ("TripId")
                ON DELETE CASCADE,
            FOREIGN KEY ("TrainDepartureId")
                REFERENCES "TrainDepartures" ("TrainDepartureId")
                ON DELETE RESTRICT
        );

        CREATE INDEX IF NOT EXISTS
            "IX_TrainReservations_TripId"
            ON "TrainReservations" ("TripId");

        CREATE UNIQUE INDEX IF NOT EXISTS
            "IX_TrainReservations_BookingCode"
            ON "TrainReservations" ("BookingCode");

        CREATE UNIQUE INDEX IF NOT EXISTS
            "UX_TrainReservation_Seat"
            ON "TrainReservations"
            ("TrainDepartureId", "WagonNumber", "SeatNumber")
            WHERE "Status" = 'Confirmed';
        """;

    public static async Task InitializeSqliteAsync(
        ApplicationDbContext context)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync()
                .ConfigureAwait(false);

        await context.Database.ExecuteSqlRawAsync(
            CreateTablesSql)
            .ConfigureAwait(false);

        await SeedDeparturesAsync(context)
            .ConfigureAwait(false);

        await transaction.CommitAsync()
            .ConfigureAwait(false);
    }

    private static async Task SeedDeparturesAsync(
        ApplicationDbContext context)
    {
        DateTime firstDay = DateTime.SpecifyKind(
            DateTime.Today,
            DateTimeKind.Utc);

        DateTime lastDay = firstDay.AddDays(60);

        var savedDepartures = await context.TrainDepartures
            .AsNoTracking()
            .Where(departure =>
                departure.DepartureAt >= firstDay
                && departure.DepartureAt < lastDay)
            .Select(departure => new
            {
                departure.TrainNumber,
                departure.DepartureAt
            })
            .ToListAsync()
            .ConfigureAwait(false);

        var existing = savedDepartures
            .Select(departure => (
                departure.TrainNumber,
                departure.DepartureAt))
            .ToHashSet();

        var routes = new[]
        {
            new DemoRoute(
                "ST101", "Львів", "Київ", 8, 7, 600m),

            new DemoRoute(
                "ST102", "Київ", "Львів", 9, 7, 600m),

            new DemoRoute(
                "ST201", "Львів", "Одеса", 8, 10, 750m),

            new DemoRoute(
                "ST202", "Одеса", "Львів", 9, 10, 750m),

            new DemoRoute(
                "ST301", "Київ", "Одеса", 8, 8, 650m),

            new DemoRoute(
                "ST302", "Одеса", "Київ", 9, 8, 650m)
        };

        for (int day = 0; day < 60; day++)
        {
            foreach (DemoRoute route in routes)
            {
                DateTime departureAt = firstDay
                    .AddDays(day)
                    .AddHours(route.DepartureHour);

                if (!existing.Add((
                    route.TrainNumber,
                    departureAt)))
                {
                    continue;
                }

                context.TrainDepartures.Add(
                    new TrainDeparture
                    {
                        TrainNumber = route.TrainNumber,
                        DepartureCity = route.DepartureCity,
                        ArrivalCity = route.ArrivalCity,
                        DepartureAt = departureAt,
                        ArrivalAt = departureAt.AddHours(
                            route.DurationHours),
                        WagonCount = 3,
                        SeatsPerWagon = 20,
                        TicketPrice = route.TicketPrice
                    });
            }
        }

        await context.SaveChangesAsync()
            .ConfigureAwait(false);
    }

    private sealed record DemoRoute(
        string TrainNumber,
        string DepartureCity,
        string ArrivalCity,
        int DepartureHour,
        int DurationHours,
        decimal TicketPrice);
}