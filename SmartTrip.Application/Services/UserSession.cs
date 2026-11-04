using SmartTrip.Application.Common;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class UserSession
{
    public User? CurrentUser { get; private set; }

    public void SignIn(User user)
    {
        CurrentUser = user;
    }

    public void SignOut()
    {
        CurrentUser = null;
    }

    public Result<User> TryRequireUser()
    {
        User? user = CurrentUser;

        if (user is null)
        {
            return Result<User>.Failure(
                "Спочатку увійдіть у систему.");
        }

        return Result<User>.Success(user);
    }

    public Result<User> TryRequireRole(string role)
    {
        Result<User> current = TryRequireUser();

        if (current.IsFailure)
        {
            return current;
        }

        User user = current.Value!;

        if (user.Role != role)
        {
            return Result<User>.Failure(
                "Ця дія недоступна для вашої ролі.");
        }

        return Result<User>.Success(user);
    }

}
