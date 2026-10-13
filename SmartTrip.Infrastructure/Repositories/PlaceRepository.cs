using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class PlaceRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : EfRepository<Place>(factory);