using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartTrip.Application.Contracts;
using SmartTrip.Infrastructure.Data;

namespace SmartTrip.Infrastructure.Repositories;

public class EfRepository<T>(
    IDbContextFactory<ApplicationDbContext> factory)
    : IRepository<T> where T : class
{
    public async Task<T?> FindAsync(int id)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        return await context.Set<T>()
            .FindAsync(id)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? filter = null)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        IQueryable<T> query = context.Set<T>().AsNoTracking();

        if (filter is not null)
        {
            query = query.Where(filter);
        }

        return await query.ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task AddAsync(T entity)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        context.Set<T>().Add(entity);

        await context.SaveChangesAsync()
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(T entity)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        context.Set<T>().Update(entity);

        await context.SaveChangesAsync()
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(T entity)
    {
        await using ApplicationDbContext context =
            await factory.CreateDbContextAsync()
                .ConfigureAwait(false);

        context.Set<T>().Remove(entity);

        await context.SaveChangesAsync()
            .ConfigureAwait(false);
    }
}