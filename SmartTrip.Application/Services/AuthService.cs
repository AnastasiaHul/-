using System.Net.Mail;
using System.Security.Cryptography;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class AuthService(
    IRepository<User> users,
    UserSession session,
    ServiceOperations operations)
{
    public Task<User> RegisterAsync(
        string email,
        string password,
        string name,
        string role)
    {
        return operations.RunAsync("Register", async () =>
        {
            string normalized = NormalizeEmail(email);

            if (password.Length < 10 || password.Length > 200)
            {
                throw new ArgumentException(
                    "Пароль повинен містити від 10 до 200 символів.");
            }

            string firstName = ServiceOperations.Required(
                name, "Ім’я", 100);

            if (role != "Tourist" && role != "Partner")
            {
                throw new ArgumentException("Невідома роль.");
            }

            IReadOnlyList<User> existing =
                await users.ListAsync(
                    x => x.Email == normalized)
                .ConfigureAwait(false);

            if (existing.Count != 0)
            {
                throw new ArgumentException(
                    "Цей email уже зареєстровано.");
            }

            var user = new User
            {
                Email = normalized,
                PasswordHash = Hash(password),
                FirstName = firstName,
                Role = role
            };

            await users.AddAsync(user)
                .ConfigureAwait(false);

            session.SignIn(user);

            return user;
        });
    }

    public Task<User> LoginAsync(
        string email,
        string password)
    {
        return operations.RunAsync("Login", async () =>
        {
            string normalized = NormalizeEmail(email);

            User? user =
                (await users.ListAsync(
                    x => x.Email == normalized)
                .ConfigureAwait(false))
                .SingleOrDefault();

            if (user is null
                || !Verify(password, user.PasswordHash))
            {
                throw new ArgumentException(
                    "Неправильний email або пароль.");
            }

            session.SignIn(user);

            return user;
        });
    }

    public Task LogoutAsync()
    {
        return operations.RunAsync("Logout", () =>
        {
            session.RequireUser();
            session.SignOut();

            return Task.CompletedTask;
        });
    }

    private static string NormalizeEmail(string email)
    {
        string normalized = ServiceOperations.Required(
            email, "Email", 254).ToLowerInvariant();

        if (!MailAddress.TryCreate(
                normalized, out MailAddress? address)
            || address.Address != normalized)
        {
            throw new ArgumentException(
                "Введіть коректний email.");
        }

        return normalized;
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
                actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}