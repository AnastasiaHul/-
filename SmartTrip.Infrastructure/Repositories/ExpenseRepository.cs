using Microsoft.EntityFrameworkCore;
using SmartTrip.Domain.Entities;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public sealed class ExpenseRepository(
    IDbContextFactory<ApplicationDbContext> factory)
    : EfRepository<Expense>(factory);