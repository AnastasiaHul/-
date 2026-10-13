using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class TripRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : EfRepository<Trip>(factory);