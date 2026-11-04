using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;

namespace SmartTrip.Application.Services;

public sealed class ServiceOperations(
    IAppLogger logger,
    UserSession session)
{
    public async Task<Result<T>> RunResultAsync<T>(
        string action,
        Func<Task<Result<T>>> work)
    {
        int? actorId = session.CurrentUser?.UserId;

        try
        {
            Result<T> result = await work()
                .ConfigureAwait(false);

            LogResult(
                action,
                result.IsSuccess,
                result.Error,
                session.CurrentUser?.UserId ?? actorId);

            return result;
        }
        catch (Exception exception)
        {
            logger.Error(exception, action, actorId);
            throw;
        }
    }

    public async Task<Result> RunResultAsync(
        string action,
        Func<Task<Result>> work)
    {
        int? actorId = session.CurrentUser?.UserId;

        try
        {
            Result result = await work()
                .ConfigureAwait(false);

            LogResult(
                action,
                result.IsSuccess,
                result.Error,
                session.CurrentUser?.UserId ?? actorId);

            return result;
        }
        catch (Exception exception)
        {
            logger.Error(exception, action, actorId);
            throw;
        }
    }

    public static Result<string> TryRequired(
        string? value,
        string field,
        int max = 200)
    {
        string result = value?.Trim() ?? string.Empty;

        if (result.Length == 0 || result.Length > max)
        {
            return Result<string>.Failure(
                $"{field}: введіть від 1 до {max} символів.");
        }

        return Result<string>.Success(result);
    }

    public static Result<decimal> TryMoney(
        decimal value,
        bool allowZero = true)
    {
        if (value < 0
            || (!allowZero && value == 0)
            || value > 100000000
            || decimal.Round(value, 2) != value)
        {
            return Result<decimal>.Failure(
                "Сума має бути невід’ємною, не перевищувати "
                + "100 000 000 і мати максимум 2 знаки після коми. "
                + "Для витрати сума має бути більшою за нуль.");
        }

        return Result<decimal>.Success(value);
    }

    private void LogResult(
        string action,
        bool isSuccess,
        string error,
        int? actorId)
    {
        string message = isSuccess
            ? action
            : $"{action}.ValidationFailed: {error}";

        logger.Information(message, actorId);
    }
}