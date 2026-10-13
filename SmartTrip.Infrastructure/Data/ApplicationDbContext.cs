using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<PackingItem> PackingItems => Set<PackingItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<Trip>()
            .HasOne(x => x.User)
            .WithMany(x => x.Trips)
            .HasForeignKey(x => x.UserId);

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
    }
}