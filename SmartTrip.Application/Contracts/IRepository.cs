using System.Linq.Expressions;

namespace SmartTrip.Application.Contracts;

public interface IRepository<T> where T : class
{
    Task<T?> FindAsync(int id);

    Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? filter = null);

    Task AddAsync(T entity);

    Task UpdateAsync(T entity);

    Task DeleteAsync(T entity);
}