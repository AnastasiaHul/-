using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;
using SmartTrip.Infrastructure.Logging;
using SmartTrip.Infrastructure.Repositories;

namespace SmartTrip.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSmartTrip(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure,
        Serilog.ILogger logger)
    {
        services.AddDbContextFactory<ApplicationDbContext>(
            configure);

        services.AddSingleton<IAppLogger>(
            new SerilogAppLogger(logger));

        services.AddScoped<UserSession>();
        services.AddScoped<ServiceOperations>();

        services.AddScoped<AuthService>();
        services.AddScoped<TripService>();
        services.AddScoped<PlaceService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<PackingService>();

        services.AddScoped<IRepository<User>, UserRepository>();
        services.AddScoped<IRepository<Trip>, TripRepository>();
        services.AddScoped<IRepository<Place>, PlaceRepository>();

        services.AddScoped<
            IRepository<Favorite>, FavoriteRepository>();

        services.AddScoped<
            IRepository<Review>, ReviewRepository>();

        services.AddScoped<
            IRepository<Expense>, ExpenseRepository>();

        services.AddScoped<
            IRepository<PackingItem>, PackingItemRepository>();

        return services;
    }
}