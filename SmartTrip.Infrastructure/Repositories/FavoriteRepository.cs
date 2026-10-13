using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class FavoriteRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : EfRepository<Favorite>(factory);