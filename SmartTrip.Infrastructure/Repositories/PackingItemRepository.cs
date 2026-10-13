using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class PackingItemRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : EfRepository<PackingItem>(factory);