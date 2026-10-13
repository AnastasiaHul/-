using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class ReviewRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : EfRepository<Review>(factory);