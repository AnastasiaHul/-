using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Infrastructure.Data;

public static class CarDatabaseInitializer
{
    private const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS "RentalCars" (
            "RentalCarId" INTEGER NOT NULL
                CONSTRAINT "PK_RentalCars"
                PRIMARY KEY AUTOINCREMENT,
            "Brand" TEXT NOT NULL,
            "Model" TEXT NOT NULL,
            "City" TEXT NOT NULL,
            "PickupAddress" TEXT NOT NULL,
            "Transmission" TEXT NOT NULL,
            "SeatsCount" INTEGER NOT NULL,
            "PricePerDay" TEXT NOT NULL,
            "IsActive" INTEGER NOT NULL,
            CONSTRAINT "CK_RentalCar_Seats"
                CHECK ("SeatsCount" > 0)
        );

        CREATE INDEX IF NOT EXISTS
            "IX_RentalCars_City_IsActive"
            ON "RentalCars" ("City", "IsActive");

        CREATE TABLE IF NOT EXISTS "CarReservations" (
            "CarReservationId" INTEGER NOT NULL
                CONSTRAINT "PK_CarReservations"
                PRIMARY KEY AUTOINCREMENT,
            "TripId" INTEGER NOT NULL,
            "RentalCarId" INTEGER NOT NULL,
            "PickupDate" TEXT NOT NULL,
            "ReturnDate" TEXT NOT NULL,
            "DriverName" TEXT NOT NULL,
            "DriverAge" INTEGER NOT NULL,
            "BookingCode" TEXT NOT NULL,
            "TotalPrice" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            CONSTRAINT "CK_CarReservation_Dates"
                CHECK ("ReturnDate" > "PickupDate"),
            CONSTRAINT "CK_CarReservation_DriverAge"
                CHECK ("DriverAge" >= 21 AND "DriverAge" <= 100),
            CONSTRAINT "FK_CarReservations_Trips_TripId"
                FOREIGN KEY ("TripId")
                REFERENCES "Trips" ("TripId")
                ON DELETE CASCADE,
            CONSTRAINT "FK_CarReservations_RentalCars_RentalCarId"
                FOREIGN KEY ("RentalCarId")
                REFERENCES "RentalCars" ("RentalCarId")
                ON DELETE RESTRICT
        );

        CREATE INDEX IF NOT EXISTS
            "IX_CarReservations_TripId"
            ON "CarReservations" ("TripId");

        CREATE UNIQUE INDEX IF NOT EXISTS
            "IX_CarReservations_BookingCode"
            ON "CarReservations" ("BookingCode");

        CREATE INDEX IF NOT EXISTS
            "IX_CarReservations_RentalCarId_Status_PickupDate_ReturnDate"
            ON "CarReservations"
                ("RentalCarId", "Status", "PickupDate", "ReturnDate");
        """;

    public static async Task InitializeSqliteAsync(
        ApplicationDbContext context)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync()
                .ConfigureAwait(false);

        await context.Database.ExecuteSqlRawAsync(CreateTablesSql)
            .ConfigureAwait(false);

        await SeedCarsAsync(context)
            .ConfigureAwait(false);

        await transaction.CommitAsync()
            .ConfigureAwait(false);
    }

    private static async Task SeedCarsAsync(
        ApplicationDbContext context)
    {
        var saved = await context.RentalCars
            .AsNoTracking()
            .Select(x => new
            {
                x.Brand,
                x.Model,
                x.City,
                x.PickupAddress
            })
            .ToListAsync()
            .ConfigureAwait(false);

        var existing = saved
            .Select(x => (
                x.Brand,
                x.Model,
                x.City,
                x.PickupAddress))
            .ToHashSet();

        RentalCar[] examples =
        [
            new RentalCar
            {
                Brand = "Toyota",
                Model = "Corolla",
                City = "Київ",
                PickupAddress = "Київ, вул. Саксаганського, 30",
                Transmission = "Автоматична",
                SeatsCount = 5,
                PricePerDay = 1400m
            },
            new RentalCar
            {
                Brand = "Kia",
                Model = "Sportage",
                City = "Київ",
                PickupAddress = "Київ, вул. Велика Васильківська, 25",
                Transmission = "Автоматична",
                SeatsCount = 5,
                PricePerDay = 2100m
            },
            new RentalCar
            {
                Brand = "Skoda",
                Model = "Octavia",
                City = "Львів",
                PickupAddress = "Львів, вул. Городоцька, 40",
                Transmission = "Механічна",
                SeatsCount = 5,
                PricePerDay = 1200m
            },
            new RentalCar
            {
                Brand = "Volkswagen",
                Model = "Golf",
                City = "Львів",
                PickupAddress = "Львів, вул. Дорошенка, 15",
                Transmission = "Автоматична",
                SeatsCount = 5,
                PricePerDay = 1500m
            },
            new RentalCar
            {
                Brand = "Mazda",
                Model = "3",
                City = "Одеса",
                PickupAddress = "Одеса, вул. Канатна, 35",
                Transmission = "Автоматична",
                SeatsCount = 5,
                PricePerDay = 1600m
            },
            new RentalCar
            {
                Brand = "Renault",
                Model = "Duster",
                City = "Одеса",
                PickupAddress = "Одеса, вул. Дерибасівська, 12",
                Transmission = "Механічна",
                SeatsCount = 5,
                PricePerDay = 1700m
            }
        ];

        foreach (RentalCar car in examples)
        {
            if (existing.Add((
                car.Brand,
                car.Model,
                car.City,
                car.PickupAddress)))
            {
                context.RentalCars.Add(car);
            }
        }

        await context.SaveChangesAsync()
            .ConfigureAwait(false);
    }
}