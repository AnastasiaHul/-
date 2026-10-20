using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Xunit;
using Moq;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Tests.Services;

public class AuthServiceRegisterTests
{
    private readonly Mock<IRepository<User>> mockUsers;
    private readonly Mock<IAppLogger> mockLogger;
    private readonly UserSession userSession;
    private readonly ServiceOperations operations;
    private readonly AuthService authService;

    public AuthServiceRegisterTests()
    {
        mockUsers = new Mock<IRepository<User>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        authService = new AuthService(mockUsers.Object, userSession, operations);
    }

    [Fact]
    public async Task Register_WithValidData_CreatesNewTouristUser()
    {
        string email = "newuser@example.com";
        string password = "ValidPassword123";
        string name = "John Doe";
        string role = "Tourist";

        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User>());

        mockUsers
            .Setup(x => x.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        var result = await authService.RegisterAsync(email, password, name, role);

        Assert.NotNull(result);
        Assert.Equal(email.ToLowerInvariant(), result.Email);
        Assert.Equal(name, result.FirstName);
        Assert.Equal("Tourist", result.Role);
        Assert.NotEmpty(result.PasswordHash);
        
        mockUsers.Verify(
            x => x.AddAsync(It.IsAny<User>()),
            Times.Once,
            "AddAsync must be called once to save user");
    }

    [Fact]
    public async Task Register_WithValidData_CreatesNewPartnerUser()
    {
        string email = "partner@example.com";
        string password = "PartnerPassword456";
        string name = "Jane Partner";
        string role = "Partner";

        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User>());

        mockUsers
            .Setup(x => x.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        var result = await authService.RegisterAsync(email, password, name, role);

        Assert.NotNull(result);
        Assert.Equal("Partner", result.Role);
        
        mockUsers.Verify(
            x => x.AddAsync(It.IsAny<User>()),
            Times.Once);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ThrowsException()
    {
        string email = "existing@example.com";
        var existingUser = new User { Email = email.ToLowerInvariant(), FirstName = "Existing" };

        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User> { existingUser });

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => authService.RegisterAsync(email, "ValidPassword123", "New User", "Tourist"));

        Assert.Contains("уже зареєстровано", exception.Message);
        
        mockUsers.Verify(
            x => x.AddAsync(It.IsAny<User>()),
            Times.Never,
            "User should not be added when email already exists");
    }

    [Fact]
    public async Task Register_WithPasswordTooShort_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => authService.RegisterAsync(
                "user@example.com",
                "Short123",
                "User Name",
                "Tourist"));

        Assert.Contains("від 10 до 200 символів", exception.Message);
        
        mockUsers.Verify(
            x => x.AddAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task Register_WithPasswordTooLong_ThrowsException()
    {
        string longPassword = new string('A', 201);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => authService.RegisterAsync(
                "user@example.com",
                longPassword,
                "User Name",
                "Tourist"));

        Assert.Contains("від 10 до 200 символів", exception.Message);
        
        mockUsers.Verify(
            x => x.AddAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task Register_WithInvalidRole_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => authService.RegisterAsync(
                "user@example.com",
                "ValidPassword123",
                "User Name",
                "InvalidRole"));

        Assert.Contains("Невідома роль", exception.Message);
        
        mockUsers.Verify(
            x => x.AddAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => authService.RegisterAsync(
                "notanemail",
                "ValidPassword123",
                "User Name",
                "Tourist"));

        Assert.Contains("коректний email", exception.Message);
        
        mockUsers.Verify(
            x => x.AddAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task Register_WithEmptyName_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => authService.RegisterAsync(
                "user@example.com",
                "ValidPassword123",
                "",
                "Tourist"));

        Assert.Contains("введіть від 1 до", exception.Message);
        
        mockUsers.Verify(
            x => x.AddAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task Register_SignsInUserAfterCreation()
    {
        string email = "newuser@example.com";

        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User>());

        mockUsers
            .Setup(x => x.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        var result = await authService.RegisterAsync(email, "ValidPassword123", "John", "Tourist");

        Assert.NotNull(userSession.CurrentUser);
        Assert.Equal(email.ToLowerInvariant(), userSession.CurrentUser.Email);
    }

    [Fact]
    public async Task Register_NormalizesEmailToLowercase()
    {
        string email = "NewUser@Example.COM";

        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User>());

        mockUsers
            .Setup(x => x.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        var result = await authService.RegisterAsync(email, "ValidPassword123", "John", "Tourist");

        Assert.Equal("newuser@example.com", result.Email);
    }

    [Fact]
    public async Task Register_HashesPassword()
    {
        string password = "ValidPassword123";

        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User>());

        mockUsers
            .Setup(x => x.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        var result = await authService.RegisterAsync("user@example.com", password, "John", "Tourist");

        Assert.NotEqual(password, result.PasswordHash);
        Assert.StartsWith("210000:", result.PasswordHash);
        Assert.Contains(":", result.PasswordHash.Substring(6));
    }
}

public class AuthServiceLoginTests
{
    private readonly Mock<IRepository<User>> mockUsers;
    private readonly Mock<IAppLogger> mockLogger;
    private readonly UserSession userSession;
    private readonly ServiceOperations operations;
    private readonly AuthService authService;

    public AuthServiceLoginTests()
    {
        mockUsers = new Mock<IRepository<User>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        authService = new AuthService(mockUsers.Object, userSession, operations);
    }

    [Fact]
    public async Task Login_WithExistingUserButWrongPassword_ThrowsException()
    {
        string email = "user@example.com";
        
        var storedUser = new User
        {
            UserId = 1,
            Email = email.ToLowerInvariant(),
            FirstName = "John",
            Role = "Tourist",
            PasswordHash = GenerateHash("CorrectPassword123")
        };

        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User> { storedUser });

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => authService.LoginAsync(email, "WrongPassword123"));

        Assert.Contains("email або пароль", exception.Message);
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ThrowsException()
    {
        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User>());

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => authService.LoginAsync("nonexistent@example.com", "Password123"));

        Assert.Contains("email або пароль", exception.Message);
    }

    [Fact]
    public async Task Login_WithValidCredentials_SignsInUser()
    {
        string email = "user@example.com";
        string password = "ValidPassword123";
        
        var storedUser = new User
        {
            UserId = 1,
            Email = email.ToLowerInvariant(),
            FirstName = "John",
            Role = "Tourist",
            PasswordHash = GenerateHash(password)
        };

        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User> { storedUser });

        var result = await authService.LoginAsync(email, password);

        Assert.NotNull(result);
        Assert.Equal(email.ToLowerInvariant(), result.Email);
        Assert.NotNull(userSession.CurrentUser);
        Assert.Equal(1, userSession.CurrentUser.UserId);
    }

    [Fact]
    public async Task Login_NormalizesEmailToLowercase()
    {
        string email = "User@Example.COM";
        string normalizedEmail = email.ToLowerInvariant();
        string password = "ValidPassword123";
        
        var storedUser = new User
        {
            UserId = 2,
            Email = normalizedEmail,
            FirstName = "John",
            Role = "Tourist",
            PasswordHash = GenerateHash(password)
        };

        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User> { storedUser });

        var result = await authService.LoginAsync(email, password);

        Assert.Equal(normalizedEmail, result.Email);
    }

    [Fact]
    public async Task Login_WithValidCredentials_SetsCurrentUser()
    {
        string email = "user@example.com";
        string password = "ValidPassword123";
        
        var storedUser = new User
        {
            UserId = 5,
            Email = email.ToLowerInvariant(),
            FirstName = "John",
            Role = "Tourist",
            PasswordHash = GenerateHash(password)
        };

        mockUsers
            .Setup(x => x.ListAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User> { storedUser });

        await authService.LoginAsync(email, password);

        Assert.NotNull(userSession.CurrentUser);
        Assert.Equal(5, userSession.CurrentUser.UserId);
    }

    private static string GenerateHash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            210000,
            HashAlgorithmName.SHA256,
            32);

        return $"210000:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }
}

public class AuthServiceLogoutTests
{
    private readonly Mock<IRepository<User>> mockUsers;
    private readonly Mock<IAppLogger> mockLogger;
    private readonly UserSession userSession;
    private readonly ServiceOperations operations;
    private readonly AuthService authService;

    public AuthServiceLogoutTests()
    {
        mockUsers = new Mock<IRepository<User>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();
        operations = new ServiceOperations(mockLogger.Object, userSession);
        authService = new AuthService(mockUsers.Object, userSession, operations);
    }

    [Fact]
    public async Task Logout_WithAuthenticatedUser_ClearsSession()
    {
        var user = new User { UserId = 1, Email = "user@example.com", FirstName = "John" };
        userSession.SignIn(user);

        await authService.LogoutAsync();

        Assert.Null(userSession.CurrentUser);
    }

    [Fact]
    public async Task Logout_WithoutAuthenticatedUser_ThrowsException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => authService.LogoutAsync());

        Assert.Contains("увійдіть у систему", exception.Message);
    }
}