namespace SmartTrip.Application.Common;

public sealed record Failure(string Message);

public sealed class Result
{
    private Result(bool isSuccess, string error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string Error { get; }

    public static implicit operator Result(Failure failure) => Failure(failure.Message);

    public static Result Success()
    {
        return new Result(true, string.Empty);
    }

    public static Result Failure(string error)
    {
        return new Result(false, error);
    }
}

public sealed class Result<T>
{
    private Result(
        bool isSuccess,
        T? value,
        string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public string Error { get; }

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Failure failure) => Failure(failure.Message);

    public static Result<T> Success(T value)
    {
        return new Result<T>(
            true,
            value,
            string.Empty);
    }

    public static Result<T> Failure(string error)
    {
        return new Result<T>(
            false,
            default,
            error);
    }
}