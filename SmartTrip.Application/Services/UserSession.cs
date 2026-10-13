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

    public User RequireUser()
    {
        return CurrentUser
            ?? throw new InvalidOperationException(
                "Спочатку увійдіть у систему.");
    }

    public User RequireRole(string role)
    {
        User user = RequireUser();

        if (user.Role != role)
        {
            throw new UnauthorizedAccessException(
                "Ця дія недоступна для вашої ролі.");
        }

        return user;
    }
}