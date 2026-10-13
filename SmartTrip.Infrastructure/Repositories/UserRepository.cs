using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class UserRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : EfRepository<User>(factory);