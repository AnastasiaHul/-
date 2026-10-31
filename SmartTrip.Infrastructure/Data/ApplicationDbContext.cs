using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<PackingItem> PackingItems => Set<PackingItem>();
    public DbSet<ItineraryVisit> ItineraryVisits => Set<ItineraryVisit>();
    public DbSet<TrainDeparture> TrainDepartures => Set<TrainDeparture>();
    public DbSet<TrainReservation> TrainReservations => Set<TrainReservation>();
    public DbSet<Accommodation> Accommodations => Set<Accommodation>();

    public DbSet<AccommodationReservation> AccommodationReservations =>
        Set<AccommodationReservation>();

    public DbSet<RentalCar> RentalCars => Set<RentalCar>();
    public DbSet<CarReservation> CarReservations => Set<CarReservation>();
    public DbSet<ExcursionSession> ExcursionSessions => Set<ExcursionSession>();

    public DbSet<ExcursionReservation> ExcursionReservations =>
        Set<ExcursionReservation>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<Trip>()
            .HasOne(x => x.User)
            .WithMany(x => x.Trips)
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<ItineraryVisit>()
            .HasOne<Trip>()
            .WithMany()
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ItineraryVisit>()
            .HasOne<Place>()
            .WithMany()
            .HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ItineraryVisit>()
            .HasIndex(x => new { x.TripId, x.VisitAt })
            .IsUnique();

        modelBuilder.Entity<Favorite>()
            .HasIndex(x => new { x.UserId, x.PlaceId })
            .IsUnique();

        modelBuilder.Entity<Favorite>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<Favorite>()
            .HasOne<Place>()
            .WithMany()
            .HasForeignKey(x => x.PlaceId);

        modelBuilder.Entity<Review>()
            .HasIndex(x => new { x.UserId, x.PlaceId })
            .IsUnique();

        modelBuilder.Entity<Review>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<Review>()
            .HasOne<Place>()
            .WithMany()
            .HasForeignKey(x => x.PlaceId);

        modelBuilder.Entity<Expense>()
            .HasOne<Trip>()
            .WithMany()
            .HasForeignKey(x => x.TripId);

        modelBuilder.Entity<Expense>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PackingItem>()
            .HasOne<Trip>()
            .WithMany()
            .HasForeignKey(x => x.TripId);

        modelBuilder.Entity<PackingItem>()
            .HasIndex(x => new { x.TripId, x.Name })
            .IsUnique();

        modelBuilder.Entity<TrainDeparture>()
            .ToTable(
                "TrainDepartures",
                table => table.HasCheckConstraint(
                    "CK_TrainDeparture_Capacity",
                    "\"WagonCount\" > 0 AND \"SeatsPerWagon\" > 0"));

        modelBuilder.Entity<TrainDeparture>()
            .Property(x => x.TrainNumber)
            .IsRequired().HasMaxLength(30);

        modelBuilder.Entity<TrainDeparture>()
            .Property(x => x.DepartureCity)
            .IsRequired().HasMaxLength(100);

        modelBuilder.Entity<TrainDeparture>()
            .Property(x => x.ArrivalCity)
            .IsRequired().HasMaxLength(100);

        modelBuilder.Entity<TrainDeparture>()
            .Property(x => x.TicketPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<TrainDeparture>()
            .HasIndex(x => new
            {
                x.DepartureCity,
                x.ArrivalCity,
                x.DepartureAt
            });

        modelBuilder.Entity<TrainReservation>()
            .ToTable(
                "TrainReservations",
                table => table.HasCheckConstraint(
                    "CK_TrainReservation_SeatNumbers",
                    "\"WagonNumber\" > 0 AND \"SeatNumber\" > 0"));

        modelBuilder.Entity<TrainReservation>()
            .HasOne<Trip>()
            .WithMany()
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrainReservation>()
            .HasOne<TrainDeparture>()
            .WithMany()
            .HasForeignKey(x => x.TrainDepartureId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainReservation>()
            .Property(x => x.PassengerName)
            .IsRequired().HasMaxLength(200);

        modelBuilder.Entity<TrainReservation>()
            .Property(x => x.BookingCode)
            .IsRequired().HasMaxLength(40);

        modelBuilder.Entity<TrainReservation>()
            .Property(x => x.Status)
            .IsRequired().HasMaxLength(20);

        modelBuilder.Entity<TrainReservation>()
            .Property(x => x.TotalPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<TrainReservation>()
            .HasIndex(x => x.BookingCode)
            .IsUnique();

        modelBuilder.Entity<TrainReservation>()
            .HasIndex(x => new
            {
                x.TrainDepartureId,
                x.WagonNumber,
                x.SeatNumber
            })
            .HasDatabaseName("UX_TrainReservation_Seat")
            .HasFilter("\"Status\" = 'Confirmed'")
            .IsUnique();

        modelBuilder.Entity<Accommodation>()
            .ToTable(
                "Accommodations",
                table => table.HasCheckConstraint(
                    "CK_Accommodation_MaxGuests",
                    "\"MaxGuests\" > 0"));

        modelBuilder.Entity<Accommodation>()
            .Property(x => x.Name)
            .IsRequired().HasMaxLength(200);

        modelBuilder.Entity<Accommodation>()
            .Property(x => x.City)
            .IsRequired().HasMaxLength(100);

        modelBuilder.Entity<Accommodation>()
            .Property(x => x.Address)
            .IsRequired().HasMaxLength(300);

        modelBuilder.Entity<Accommodation>()
            .Property(x => x.Description)
            .IsRequired().HasMaxLength(2000);

        modelBuilder.Entity<Accommodation>()
            .Property(x => x.PricePerNight)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Accommodation>()
            .HasIndex(x => new { x.City, x.IsActive });

        modelBuilder.Entity<AccommodationReservation>()
            .ToTable(
                "AccommodationReservations",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_AccommodationReservation_Dates",
                        "\"CheckOut\" > \"CheckIn\"");

                    table.HasCheckConstraint(
                        "CK_AccommodationReservation_Guests",
                        "\"GuestsCount\" > 0");
                });

        modelBuilder.Entity<AccommodationReservation>()
            .HasOne<Trip>()
            .WithMany()
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AccommodationReservation>()
            .HasOne<Accommodation>()
            .WithMany()
            .HasForeignKey(x => x.AccommodationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AccommodationReservation>()
            .Property(x => x.GuestName)
            .IsRequired().HasMaxLength(200);

        modelBuilder.Entity<AccommodationReservation>()
            .Property(x => x.BookingCode)
            .IsRequired().HasMaxLength(40);

        modelBuilder.Entity<AccommodationReservation>()
            .Property(x => x.Status)
            .IsRequired().HasMaxLength(20);

        modelBuilder.Entity<AccommodationReservation>()
            .Property(x => x.TotalPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<AccommodationReservation>()
            .HasIndex(x => x.BookingCode)
            .IsUnique();

        modelBuilder.Entity<AccommodationReservation>()
            .HasIndex(x => new
            {
                x.AccommodationId,
                x.Status,
                x.CheckIn,
                x.CheckOut
            });

        modelBuilder.Entity<RentalCar>()
            .ToTable(
                "RentalCars",
                table => table.HasCheckConstraint(
                    "CK_RentalCar_Seats",
                    "\"SeatsCount\" > 0"));

        modelBuilder.Entity<RentalCar>()
            .Property(x => x.Brand)
            .IsRequired().HasMaxLength(100);

        modelBuilder.Entity<RentalCar>()
            .Property(x => x.Model)
            .IsRequired().HasMaxLength(100);

        modelBuilder.Entity<RentalCar>()
            .Property(x => x.City)
            .IsRequired().HasMaxLength(100);

        modelBuilder.Entity<RentalCar>()
            .Property(x => x.PickupAddress)
            .IsRequired().HasMaxLength(300);

        modelBuilder.Entity<RentalCar>()
            .Property(x => x.Transmission)
            .IsRequired().HasMaxLength(30);

        modelBuilder.Entity<RentalCar>()
            .Property(x => x.PricePerDay)
            .HasPrecision(18, 2);

        modelBuilder.Entity<RentalCar>()
            .HasIndex(x => new { x.City, x.IsActive });

        modelBuilder.Entity<CarReservation>()
            .ToTable(
                "CarReservations",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_CarReservation_Dates",
                        "\"ReturnDate\" > \"PickupDate\"");

                    table.HasCheckConstraint(
                        "CK_CarReservation_DriverAge",
                        "\"DriverAge\" >= 21 AND \"DriverAge\" <= 100");
                });

        modelBuilder.Entity<CarReservation>()
            .HasOne<Trip>()
            .WithMany()
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CarReservation>()
            .HasOne<RentalCar>()
            .WithMany()
            .HasForeignKey(x => x.RentalCarId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CarReservation>()
            .Property(x => x.DriverName)
            .IsRequired().HasMaxLength(200);

        modelBuilder.Entity<CarReservation>()
            .Property(x => x.BookingCode)
            .IsRequired().HasMaxLength(40);

        modelBuilder.Entity<CarReservation>()
            .Property(x => x.Status)
            .IsRequired().HasMaxLength(20);

        modelBuilder.Entity<CarReservation>()
            .Property(x => x.TotalPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<CarReservation>()
            .HasIndex(x => x.BookingCode)
            .IsUnique();

        modelBuilder.Entity<CarReservation>()
            .HasIndex(x => new
            {
                x.RentalCarId,
                x.Status,
                x.PickupDate,
                x.ReturnDate
            });

        modelBuilder.Entity<ExcursionSession>()
            .ToTable(
                "ExcursionSessions",
                table => table.HasCheckConstraint(
                    "CK_ExcursionSession_Settings",
                    "\"Capacity\" > 0 AND \"DurationMinutes\" > 0"));

        modelBuilder.Entity<ExcursionSession>()
            .Property(x => x.Title)
            .IsRequired().HasMaxLength(200);

        modelBuilder.Entity<ExcursionSession>()
            .Property(x => x.City)
            .IsRequired().HasMaxLength(100);

        modelBuilder.Entity<ExcursionSession>()
            .Property(x => x.MeetingAddress)
            .IsRequired().HasMaxLength(300);

        modelBuilder.Entity<ExcursionSession>()
            .Property(x => x.Description)
            .IsRequired().HasMaxLength(2000);

        modelBuilder.Entity<ExcursionSession>()
            .Property(x => x.PricePerPerson)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ExcursionSession>()
            .HasIndex(x => new
            {
                x.City,
                x.StartsAt,
                x.IsActive
            });

        modelBuilder.Entity<ExcursionReservation>()
            .ToTable(
                "ExcursionReservations",
                table => table.HasCheckConstraint(
                    "CK_ExcursionReservation_Participants",
                    "\"ParticipantsCount\" > 0"));

        modelBuilder.Entity<ExcursionReservation>()
            .HasOne<Trip>()
            .WithMany()
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExcursionReservation>()
            .HasOne<ExcursionSession>()
            .WithMany()
            .HasForeignKey(x => x.ExcursionSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExcursionReservation>()
            .Property(x => x.GuestName)
            .IsRequired().HasMaxLength(200);

        modelBuilder.Entity<ExcursionReservation>()
            .Property(x => x.BookingCode)
            .IsRequired().HasMaxLength(40);

        modelBuilder.Entity<ExcursionReservation>()
            .Property(x => x.Status)
            .IsRequired().HasMaxLength(20);

        modelBuilder.Entity<ExcursionReservation>()
            .Property(x => x.TotalPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ExcursionReservation>()
            .HasIndex(x => x.BookingCode)
            .IsUnique();

        modelBuilder.Entity<ExcursionReservation>()
            .HasIndex(x => new
            {
                x.ExcursionSessionId,
                x.Status
            });
    }
}