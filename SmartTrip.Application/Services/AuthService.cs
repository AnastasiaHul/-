using System.Net.Mail;
using System.Security.Cryptography;
using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class AuthService(
    IRepository<User> users,
    UserSession session,
    ServiceOperations operations)
{
    public Task<Result<User>> RegisterAsync(
        string? email,
        string? password,
        string? name,
        string? role)
    {
        return operations.RunResultAsync<User>(
            "Register",
            async () =>
            {
                Result<string> normalized = NormalizeEmail(email);

                if (normalized.IsFailure)
                {
                    return Result<User>.Failure(normalized.Error);
                }

                if (password is null
                    || password.Length < 10
                    || password.Length > 200)
                {
                    return Result<User>.Failure(
                        "Пароль повинен містити від 10 до 200 символів.");
                }

                Result<string> firstName =
                    ServiceOperations.TryRequired(
                        name,
                        "Ім’я",
                        100);

                if (firstName.IsFailure)
                {
                    return Result<User>.Failure(firstName.Error);
                }

                if (role != "Tourist" && role != "Partner")
                {
                    return Result<User>.Failure(
                        "Оберіть роль: турист або партнер.");
                }

                string normalizedEmail = normalized.Value!;

                IReadOnlyList<User> existing =
                    await users.ListAsync(
                        x => x.Email == normalizedEmail)
                    .ConfigureAwait(false);

                if (existing.Count != 0)
                {
                    return Result<User>.Failure(
                        "Цей email уже зареєстровано.");
                }

                var user = new User
                {
                    Email = normalizedEmail,
                    PasswordHash = Hash(password),
                    FirstName = firstName.Value!,
                    Role = role
                };

                await users.AddAsync(user)
                    .ConfigureAwait(false);

                session.SignIn(user);

                return Result<User>.Success(user);
            });
    }

    public Task<Result<User>> LoginAsync(
        string? email,
        string? password)
    {
        return operations.RunResultAsync<User>(
            "Login",
            async () =>
            {
                Result<string> normalized = NormalizeEmail(email);

                if (normalized.IsFailure)
                {
                    return Result<User>.Failure(normalized.Error);
                }

                if (string.IsNullOrEmpty(password)
                    || password.Length > 200)
                {
                    return Result<User>.Failure(
                        "Неправильний email або пароль.");
                }

                string normalizedEmail = normalized.Value!;

                IReadOnlyList<User> found =
                    await users.ListAsync(
                        x => x.Email == normalizedEmail)
                    .ConfigureAwait(false);

                User? user = found.SingleOrDefault();

                if (user is null
                    || !Verify(password, user.PasswordHash))
                {
                    return Result<User>.Failure(
                        "Неправильний email або пароль.");
                }

                session.SignIn(user);

                return Result<User>.Success(user);
            });
    }

    public Task<Result> LogoutAsync()
    {
        return operations.RunResultAsync(
            "Logout",
            () =>
            {
                Result<User> current = session.TryRequireUser();

                if (current.IsFailure)
                {
                    return Task.FromResult(
                        Result.Failure(current.Error));
                }

                session.SignOut();

                return Task.FromResult(Result.Success());
            });
    }

    private static Result<string> NormalizeEmail(string? email)
    {
        Result<string> required =
            ServiceOperations.TryRequired(
                email,
                "Email",
                254);

        if (required.IsFailure)
        {
            return Result<string>.Failure(required.Error);
        }

        string normalized = required.Value!.ToLowerInvariant();

        if (!MailAddress.TryCreate(
                normalized,
                out MailAddress? address)
            || address.Address != normalized)
        {
            return Result<string>.Failure(
                "Введіть коректний email.");
        }

        return Result<string>.Success(normalized);
    }

    private static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            210000,
            HashAlgorithmName.SHA256,
            32);

        return $"210000:{Convert.ToBase64String(salt)}"
            + $":{Convert.ToBase64String(hash)}";
    }

    private static bool Verify(
        string password,
        string stored)
    {
        string[] parts = stored.Split(':');

        if (parts.Length != 3
            || !int.TryParse(parts[0], out int iterations)
            || iterations != 210000)
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expected = Convert.FromBase64String(parts[2]);

            if (salt.Length != 16 || expected.Length != 32)
            {
                return false;
            }

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                32);

            return CryptographicOperations.FixedTimeEquals(
                actual,
                expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}