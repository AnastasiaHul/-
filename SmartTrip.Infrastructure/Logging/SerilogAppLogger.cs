using Serilog;
using SmartTrip.Application.Contracts;

namespace SmartTrip.Infrastructure.Logging;

public sealed class SerilogAppLogger(ILogger logger)
    : IAppLogger
{
    public void Information(string action, int? userId)
    {
        logger.Information(
            "Action {Action} succeeded; actor {UserId}",
            action,
            userId);
    }

    public void Error(
        Exception exception,
        string action,
        int? userId)
    {
        logger.Error(
            exception,
            "Action {Action} failed; actor {UserId}",
            action,
            userId);
    }
}