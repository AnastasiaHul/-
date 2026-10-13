using SmartTrip.Application.Contracts;

namespace SmartTrip.Application.Services;

public sealed class ServiceOperations(
    IAppLogger logger,
    UserSession session)
{
    public async Task<T> RunAsync<T>(
        string action,
        Func<Task<T>> work)
    {
        int? actorId = session.CurrentUser?.UserId;

        try
        {
            T result = await work().ConfigureAwait(false);

            logger.Information(
                action,
                session.CurrentUser?.UserId ?? actorId);

            return result;
        }
        catch (Exception exception)
        {
            logger.Error(exception, action, actorId);
            throw;
        }
    }

    public Task RunAsync(string action, Func<Task> work)
    {
        return RunAsync(action, async () =>
        {
            await work().ConfigureAwait(false);
            return true;
        });
    }

    public static string Required(
        string? value,
        string field,
        int max = 200)
    {
        string result = value?.Trim() ?? string.Empty;

        if (result.Length == 0 || result.Length > max)
        {
            throw new ArgumentException(
                $"{field}: введіть від 1 до {max} символів.");
        }

        return result;
    }

    public static decimal Money(
        decimal value,
        bool allowZero = true)
    {
        if (value < 0
            || (!allowZero && value == 0)
            || value > 100000000
            || decimal.Round(value, 2) != value)
        {
            throw new ArgumentException(
                "Сума має бути невід’ємною, не перевищувати "
                + "100 000 000 і мати максимум 2 знаки після коми. "
                + "Для витрати сума має бути більшою за нуль.");
        }

        return value;
    }
}