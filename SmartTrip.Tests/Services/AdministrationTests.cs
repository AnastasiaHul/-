using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;
using SmartTrip.Infrastructure.Repositories;
using Xunit;

namespace SmartTrip.Tests.Services;

public sealed class AdministrationTests : IDisposable
{
    private const string Password = "Coursework-demo-password";
    private readonly string database = Path.Combine(Path.GetTempPath(), $"smarttrip-admin-{Guid.NewGuid():N}.db");
    private readonly Mock<IAppLogger> log = new();

    [Fact]
    public async Task PartnerSubmits_AdminReturnsAndApproves_OnlyThenTouristSeesPlace()
    {
        var factory = await InitializeAsync();
        var places = new PlaceRepository(factory);
        var partner = Session(2, "Partner");
        var placeService = new PlaceService(places, partner, new ServiceOperations(log.Object, partner));
        var tourist = Session(3, "Tourist");
        var catalog = new CatalogService(places, new FavoriteRepository(factory), new ReviewRepository(factory),
            tourist, new ServiceOperations(log.Object, tourist));
        var admin = Admin(factory);

        var created = await placeService.SaveAsync(null, "Музей", "Музеї", 100, "Львів, площа Ринок, 1", null, null);
        Assert.True(created.IsSuccess);
        int id = created.Value!.PlaceId;
        Assert.Empty((await catalog.SearchAsync("", "", null, false)).Value!);
        Assert.Single((await admin.ListPendingAsync()).Value!);
        Assert.True((await admin.ReviewPlaceAsync(id, false, "Уточніть адресу.")).IsSuccess);
        Assert.Equal("Уточніть адресу.", (await placeService.ListOwnAsync()).Value!.Single().ModerationReason);

        Assert.True((await placeService.SaveAsync(id, "Музей", "Музеї", 100,
            "Львів, площа Ринок, 2", null, null)).IsSuccess);
        Assert.True((await admin.ReviewPlaceAsync(id, true, "Адресу перевірено.")).IsSuccess);
        Assert.Equal(id, (await catalog.SearchAsync("", "", null, false)).Value!.Single().PlaceId);
        Assert.True((await admin.ReviewPlaceAsync(id, true, "Повтор.")).IsFailure);

        var audit = (await admin.ListAuditAsync(null, null, AdministrationService.ApprovePlace)).Value!;
        Assert.Equal(id, Assert.Single(audit).TargetId);
        Assert.Equal(1, audit[0].ActorId);
        Assert.Equal("Адресу перевірено.", audit[0].Reason);
    }

    [Fact]
    public async Task BlockedUserCannotLoginUntilUnblocked_RegistrationCannotChooseAdmin()
    {
        var factory = await InitializeAsync();
        var session = new UserSession();
        var auth = new AuthService(new UserRepository(factory), session, new ServiceOperations(log.Object, session));
        Assert.True((await auth.RegisterAsync("root@example.org", Password, "Test", "Admin")).IsFailure);
        var registered = await auth.RegisterAsync("tourist@example.org", Password, "Test", "Tourist");
        int id = registered.Value!.UserId;
        session.SignOut();

        var admin = Admin(factory);
        Assert.Equal(id, (await admin.FindAccountAsync(" TOURIST@example.org ")).Value!.UserId);
        Assert.True((await admin.SetAccountBlockedAsync(id, true, "Перевірка скарги.")).IsSuccess);
        Assert.True((await auth.LoginAsync("tourist@example.org", Password)).IsFailure);
        Assert.Null(session.CurrentUser);
        Assert.True((await admin.SetAccountBlockedAsync(id, false, "Перевірку завершено.")).IsSuccess);
        Assert.True((await auth.LoginAsync("tourist@example.org", Password)).IsSuccess);
        Assert.True((await admin.SetAccountBlockedAsync(1, true, "Спроба заблокувати адміністратора.")).IsFailure);
    }

    [Fact]
    public async Task CancellationPreservesReservationAndReason_AndReleasesSeats()
    {
        var factory = await InitializeAsync();
        await using (var db = factory.CreateDbContext())
        {
            db.Users.Add(new User { UserId = 2, Email = "traveler@example.org" });
            db.Trips.Add(new Trip { TripId = 2, UserId = 2 });
            db.ExcursionSessions.Add(new ExcursionSession
            {
                ExcursionSessionId = 1, DurationMinutes = 60, Capacity = 10
            });
            await db.SaveChangesAsync();
            db.ExcursionReservations.Add(new ExcursionReservation
            {
                ExcursionReservationId = 1, TripId = 2, ExcursionSessionId = 1,
                ParticipantsCount = 2, BookingCode = "EX-test"
            });
            await db.SaveChangesAsync();
        }

        var admin = Admin(factory);
        var found = await admin.FindExcursionAsync("ex-TEST");
        Assert.Equal(1, found.Value!.ReservationId);
        var reservations = new ExcursionReservationRepository(factory);
        Assert.Equal(2L, (await reservations.GetReservedCountsAsync([1]))[1]);
        Assert.True((await admin.CancelExcursionAsync(1, "Гід скасував подію.")).IsSuccess);
        Assert.True((await admin.CancelExcursionAsync(1, "Повторне скасування.")).IsFailure);

        var saved = Assert.Single(await reservations.ListForTripAsync(2));
        Assert.Equal("Cancelled", saved.Status);
        Assert.Equal("Гід скасував подію.", saved.CancellationReason);
        Assert.Empty(await reservations.GetReservedCountsAsync([1]));
        var audit = (await admin.ListAuditAsync(DateTime.Today, DateTime.Today, null)).Value!;
        Assert.Equal(AdministrationService.CancelExcursion, Assert.Single(audit).Action);
        log.Verify(x => x.Information("Admin.Excursion.Cancel.Succeeded: target 1", 1), Times.Once);
        log.Verify(x => x.Information(It.Is<string>(s => s.StartsWith("Admin.Excursion.Cancel.ValidationFailed")), 1), Times.Once);
    }

    [Fact]
    public async Task TouristCannotAccessAdminActions()
    {
        var repository = new Mock<IAdministrationRepository>(MockBehavior.Strict);
        var admin = new AdministrationService(Session(7, "Tourist"), repository.Object, log.Object);
        Assert.True((await admin.ListPendingAsync()).IsFailure);
        Assert.True((await admin.ReviewPlaceAsync(1, true, "Дані перевірено.")).IsFailure);
        Assert.True((await admin.SetAccountBlockedAsync(8, true, "Скарга.")).IsFailure);
        Assert.True((await admin.CancelExcursionAsync(2, "Скасування.")).IsFailure);
        Assert.True((await admin.ListAuditAsync(null, null, null)).IsFailure);
        repository.VerifyNoOtherCalls();
    }

    private AdministrationService Admin(TestFactory factory) =>
        new(Session(1, "Admin"), new AdministrationRepository(factory), log.Object);

    private async Task<TestFactory> InitializeAsync()
    {
        var factory = new TestFactory(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={database};Pooling=False").Options);
        await using var db = factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await AdministrationDatabaseInitializer.InitializeSqliteAsync(factory);
        db.Users.Add(new User { UserId = 1, Email = "admin@example.org", Role = "Admin" });
        await db.SaveChangesAsync();
        return factory;
    }

    private static UserSession Session(int id, string role)
    {
        var session = new UserSession();
        session.SignIn(new User { UserId = id, Role = role });
        return session;
    }

    public void Dispose() => File.Delete(database);

    private sealed class TestFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}
