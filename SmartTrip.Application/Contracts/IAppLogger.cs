namespace SmartTrip.Application.Contracts;

public interface IAppLogger
{
    void Information(string action, int? userId);

    void Error(
        Exception exception,
        string action,
        int? userId);
}