using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Infrastructure.Data;

public static class AccommodationDatabaseInitializer
{
    private const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS "Accommodations" (
            "AccommodationId" INTEGER NOT NULL
                CONSTRAINT "PK_Accommodations"
                PRIMARY KEY AUTOINCREMENT,
            "Name" TEXT NOT NULL,
            "City" TEXT NOT NULL,
            "Address" TEXT NOT NULL,
            "Description" TEXT NOT NULL,
            "MaxGuests" INTEGER NOT NULL,
            "PricePerNight" TEXT NOT NULL,
            "IsActive" INTEGER NOT NULL,
            CONSTRAINT "CK_Accommodation_MaxGuests"
                CHECK ("MaxGuests" > 0)
        );

        CREATE INDEX IF NOT EXISTS
            "IX_Accommodations_City_IsActive"
            ON "Accommodations" ("City", "IsActive");

        CREATE TABLE IF NOT EXISTS "AccommodationReservations" (
            "AccommodationReservationId" INTEGER NOT NULL
                CONSTRAINT "PK_AccommodationReservations"
                PRIMARY KEY AUTOINCREMENT,
            "TripId" INTEGER NOT NULL,
            "AccommodationId" INTEGER NOT NULL,
            "CheckIn" TEXT NOT NULL,
            "CheckOut" TEXT NOT NULL,
            "GuestsCount" INTEGER NOT NULL,
            "GuestName" TEXT NOT NULL,
            "BookingCode" TEXT NOT NULL,
            "TotalPrice" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            CONSTRAINT "CK_AccommodationReservation_Dates"
                CHECK ("CheckOut" > "CheckIn"),
            CONSTRAINT "CK_AccommodationReservation_Guests"
                CHECK ("GuestsCount" > 0),
            CONSTRAINT "FK_AccommodationReservations_Trips_TripId"
                FOREIGN KEY ("TripId")
                REFERENCES "Trips" ("TripId")
                ON DELETE CASCADE,
            CONSTRAINT
                "FK_AccommodationReservations_Accommodations_AccommodationId"
                FOREIGN KEY ("AccommodationId")
                REFERENCES "Accommodations" ("AccommodationId")
                ON DELETE RESTRICT
        );

        CREATE INDEX IF NOT EXISTS
            "IX_AccommodationReservations_TripId"
            ON "AccommodationReservations" ("TripId");

        CREATE UNIQUE INDEX IF NOT EXISTS
            "IX_AccommodationReservations_BookingCode"
            ON "AccommodationReservations" ("BookingCode");

        CREATE INDEX IF NOT EXISTS
            "IX_AccommodationReservations_Availability"
            ON "AccommodationReservations"
                ("AccommodationId", "Status", "CheckIn", "CheckOut");
        """;

    public static async Task InitializeSqliteAsync(
        ApplicationDbContext context)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync()
                .ConfigureAwait(false);

        await context.Database.ExecuteSqlRawAsync(CreateTablesSql)
            .ConfigureAwait(false);

        await SeedAccommodationsAsync(context)
            .ConfigureAwait(false);

        await transaction.CommitAsync()
            .ConfigureAwait(false);
    }

    private static async Task SeedAccommodationsAsync(
        ApplicationDbContext context)
    {
        var saved = await context.Accommodations
            .AsNoTracking()
            .Select(x => new
            {
                x.Name,
                x.City,
                x.Address
            })
            .ToListAsync()
            .ConfigureAwait(false);

        var existing = saved
            .Select(x => (x.Name, x.City, x.Address))
            .ToHashSet();

        Accommodation[] examples =
        [
            new Accommodation
            {
                Name = "Затишна квартира у Львові",
                City = "Львів",
                Address = "Львів, вул. Дорошенка, 15",
                Description =
                    "Демонстраційна квартира для двох гостей. "
                    + "Кухня, Wi-Fi, окрема спальня.",
                MaxGuests = 2,
                PricePerNight = 1200m
            },
            new Accommodation
            {
                Name = "Сімейні апартаменти у Львові",
                City = "Львів",
                Address = "Львів, вул. Городоцька, 40",
                Description =
                    "Демонстраційні апартаменти для сім’ї. "
                    + "Дві спальні, кухня, пральна машина.",
                MaxGuests = 4,
                PricePerNight = 1900m
            },
            new Accommodation
            {
                Name = "Квартира для двох у Києві",
                City = "Київ",
                Address = "Київ, вул. Велика Васильківська, 25",
                Description =
                    "Демонстраційна квартира для двох гостей. "
                    + "Окрема кухня, Wi-Fi, робоче місце.",
                MaxGuests = 2,
                PricePerNight = 1500m
            },
            new Accommodation
            {
                Name = "Просторі апартаменти у Києві",
                City = "Київ",
                Address = "Київ, вул. Саксаганського, 30",
                Description =
                    "Демонстраційні апартаменти для компанії. "
                    + "Дві спальні, кухня, балкон.",
                MaxGuests = 4,
                PricePerNight = 2300m
            },
            new Accommodation
            {
                Name = "Студія в Одесі",
                City = "Одеса",
                Address = "Одеса, вул. Дерибасівська, 12",
                Description =
                    "Демонстраційна студія для двох гостей. "
                    + "Мінікухня, кондиціонер, Wi-Fi.",
                MaxGuests = 2,
                PricePerNight = 1300m
            },
            new Accommodation
            {
                Name = "Сімейна квартира в Одесі",
                City = "Одеса",
                Address = "Одеса, вул. Канатна, 35",
                Description =
                    "Демонстраційна квартира для сім’ї. "
                    + "Дві спальні, кухня, кондиціонер.",
                MaxGuests = 4,
                PricePerNight = 2000m
            }
        ];

        foreach (Accommodation accommodation in examples)
        {
            if (existing.Add((
                accommodation.Name,
                accommodation.City,
                accommodation.Address)))
            {
                context.Accommodations.Add(accommodation);
            }
        }

        await context.SaveChangesAsync()
            .ConfigureAwait(false);
    }
}