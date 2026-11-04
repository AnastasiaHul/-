using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Moq;
using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using Xunit;

namespace SmartTrip.Tests.Services;

public class AuthServiceRegisterTests
{
    private readonly Mock<IRepository<User>> mockUsers;
    private readonly Mock<IAppLogger> mockLogger;
    private readonly UserSession userSession;
    private readonly AuthService authService;

    public AuthServiceRegisterTests()
    {
        mockUsers = new Mock<IRepository<User>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();

        var operations = new ServiceOperations(
            mockLogger.Object,
            userSession);

        authService = new AuthService(
            mockUsers.Object,
            userSession,
            operations);

        mockUsers
            .Setup(x => x.ListAsync(
                It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User>());

        mockUsers
            .Setup(x => x.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Register_WithValidData_CreatesNewTouristUser()
    {
        Result<User> result = await authService.RegisterAsync(
            "newuser@example.com",
            "ValidPassword123",
            "John Doe",
            "Tourist");

        User user = SuccessfulUser(result);

        Assert.Equal("newuser@example.com", user.Email);
        Assert.Equal("John Doe", user.FirstName);
        Assert.Equal("Tourist", user.Role);
        Assert.NotEmpty(user.PasswordHash);
        Assert.Same(user, userSession.CurrentUser);

        mockUsers.Verify(
            x => x.AddAsync(It.Is<User>(
                saved => ReferenceEquals(saved, user))),
            Times.Once);
    }

    [Fact]
    public async Task Register_WithValidData_CreatesNewPartnerUser()
    {
        Result<User> result = await authService.RegisterAsync(
            "partner@example.com",
            "PartnerPassword456",
            "Jane Partner",
            "Partner");

        User user = SuccessfulUser(result);

        Assert.Equal("Partner", user.Role);
        Assert.Equal("partner@example.com", user.Email);
        Assert.Same(user, userSession.CurrentUser);

        mockUsers.Verify(
            x => x.AddAsync(It.Is<User>(
                saved => ReferenceEquals(saved, user))),
            Times.Once);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsFailure()
    {
        var existingUser = new User
        {
            Email = "existing@example.com",
            FirstName = "Existing"
        };

        mockUsers
            .Setup(x => x.ListAsync(
                It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User> { existingUser });

        Result<User> result = await authService.RegisterAsync(
            "existing@example.com",
            "ValidPassword123",
            "New User",
            "Tourist");

        AssertRegistrationFailure(
            result,
            "уже зареєстровано");
    }

    [Fact]
    public async Task Register_WithPasswordTooShort_ReturnsFailure()
    {
        Result<User> result = await authService.RegisterAsync(
            "user@example.com",
            "Short123",
            "User Name",
            "Tourist");

        AssertRegistrationFailure(
            result,
            "від 10 до 200 символів");
    }

    [Fact]
    public async Task Register_WithPasswordTooLong_ReturnsFailure()
    {
        Result<User> result = await authService.RegisterAsync(
            "user@example.com",
            new string('A', 201),
            "User Name",
            "Tourist");

        AssertRegistrationFailure(
            result,
            "від 10 до 200 символів");
    }

    [Fact]
    public async Task Register_WithInvalidRole_ReturnsFailure()
    {
        Result<User> result = await authService.RegisterAsync(
            "user@example.com",
            "ValidPassword123",
            "User Name",
            "InvalidRole");

        AssertRegistrationFailure(
            result,
            "Оберіть роль");
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsFailure()
    {
        Result<User> result = await authService.RegisterAsync(
            "notanemail",
            "ValidPassword123",
            "User Name",
            "Tourist");

        AssertRegistrationFailure(
            result,
            "коректний email");
    }

    [Fact]
    public async Task Register_WithEmptyName_ReturnsFailure()
    {
        Result<User> result = await authService.RegisterAsync(
            "user@example.com",
            "ValidPassword123",
            "",
            "Tourist");

        AssertRegistrationFailure(
            result,
            "введіть від 1 до");
    }

    [Fact]
    public async Task Register_SignsInUserAfterCreation()
    {
        Result<User> result = await authService.RegisterAsync(
            "newuser@example.com",
            "ValidPassword123",
            "John",
            "Tourist");

        User user = SuccessfulUser(result);

        Assert.Same(user, userSession.CurrentUser);

        mockLogger.Verify(
            x => x.Information("Register", user.UserId),
            Times.Once);
    }

    [Fact]
    public async Task Register_NormalizesEmailToLowercase()
    {
        Result<User> result = await authService.RegisterAsync(
            "  NewUser@Example.COM  ",
            "ValidPassword123",
            "John",
            "Tourist");

        User user = SuccessfulUser(result);

        Assert.Equal("newuser@example.com", user.Email);

        var matchingUser = new User
        {
            Email = "newuser@example.com"
        };

        var differentUser = new User
        {
            Email = "different@example.com"
        };

        mockUsers.Verify(
            x => x.ListAsync(
                It.Is<Expression<Func<User, bool>>>(
                    filter => filter.Compile()(matchingUser)
                        && !filter.Compile()(differentUser))),
            Times.Once);
    }

    [Fact]
    public async Task Register_HashesPassword()
    {
        const string password = "ValidPassword123";

        Result<User> result = await authService.RegisterAsync(
            "user@example.com",
            password,
            "John",
            "Tourist");

        User user = SuccessfulUser(result);

        Assert.NotEqual(password, user.PasswordHash);
        Assert.StartsWith("210000:", user.PasswordHash);

        string[] parts = user.PasswordHash.Split(':');

        Assert.Equal(3, parts.Length);
        Assert.Equal(16, Convert.FromBase64String(parts[1]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[2]).Length);
    }

    private static User SuccessfulUser(Result<User> result)
    {
        Assert.True(result.IsSuccess, result.Error);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Error);

        return Assert.IsType<User>(result.Value);
    }

    private void AssertRegistrationFailure(
        Result<User> result,
        string expectedMessage)
    {
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Contains(expectedMessage, result.Error);
        Assert.Null(userSession.CurrentUser);

        mockUsers.Verify(
            x => x.AddAsync(It.IsAny<User>()),
            Times.Never);

        mockLogger.Verify(
            x => x.Information(
                It.Is<string>(message =>
                    message.Contains("Register.ValidationFailed")
                    && message.Contains(expectedMessage)),
                It.IsAny<int?>()),
            Times.Once);

        mockLogger.Verify(
            x => x.Error(
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<int?>()),
            Times.Never);
    }
}

public class AuthServiceLoginTests
{
    private readonly Mock<IRepository<User>> mockUsers;
    private readonly Mock<IAppLogger> mockLogger;
    private readonly UserSession userSession;
    private readonly AuthService authService;

    public AuthServiceLoginTests()
    {
        mockUsers = new Mock<IRepository<User>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();

        var operations = new ServiceOperations(
            mockLogger.Object,
            userSession);

        authService = new AuthService(
            mockUsers.Object,
            userSession,
            operations);
    }

    [Fact]
    public async Task Login_WithExistingUserButWrongPassword_ReturnsFailure()
    {
        var storedUser = new User
        {
            UserId = 1,
            Email = "user@example.com",
            FirstName = "John",
            Role = "Tourist",
            PasswordHash = GenerateHash("CorrectPassword123")
        };

        SetupUser(storedUser);

        Result<User> result = await authService.LoginAsync(
            "user@example.com",
            "WrongPassword123");

        AssertLoginFailure(result);
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ReturnsFailure()
    {
        mockUsers
            .Setup(x => x.ListAsync(
                It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User>());

        Result<User> result = await authService.LoginAsync(
            "nonexistent@example.com",
            "Password123");

        AssertLoginFailure(result);
    }

    [Fact]
    public async Task Login_WithValidCredentials_SignsInUser()
    {
        const string password = "ValidPassword123";

        var storedUser = new User
        {
            UserId = 1,
            Email = "user@example.com",
            FirstName = "John",
            Role = "Tourist",
            PasswordHash = GenerateHash(password)
        };

        SetupUser(storedUser);

        Result<User> result = await authService.LoginAsync(
            "user@example.com",
            password);

        User user = SuccessfulUser(result);

        Assert.Same(storedUser, user);
        Assert.Same(storedUser, userSession.CurrentUser);

        mockLogger.Verify(
            x => x.Information("Login", storedUser.UserId),
            Times.Once);
    }

    [Fact]
    public async Task Login_NormalizesEmailToLowercase()
    {
        const string password = "ValidPassword123";

        var storedUser = new User
        {
            UserId = 2,
            Email = "user@example.com",
            FirstName = "John",
            Role = "Tourist",
            PasswordHash = GenerateHash(password)
        };

        SetupUser(storedUser);

        Result<User> result = await authService.LoginAsync(
            "  User@Example.COM  ",
            password);

        User user = SuccessfulUser(result);

        Assert.Equal("user@example.com", user.Email);

        var differentUser = new User
        {
            Email = "different@example.com"
        };

        mockUsers.Verify(
            x => x.ListAsync(
                It.Is<Expression<Func<User, bool>>>(
                    filter => filter.Compile()(storedUser)
                        && !filter.Compile()(differentUser))),
            Times.Once);
    }

    [Fact]
    public async Task Login_WithValidCredentials_SetsCurrentUser()
    {
        const string password = "ValidPassword123";

        var storedUser = new User
        {
            UserId = 5,
            Email = "user@example.com",
            FirstName = "John",
            Role = "Tourist",
            PasswordHash = GenerateHash(password)
        };

        SetupUser(storedUser);

        Result<User> result = await authService.LoginAsync(
            "user@example.com",
            password);

        User user = SuccessfulUser(result);

        Assert.Equal(5, user.UserId);
        Assert.Same(user, userSession.CurrentUser);
    }

    private void SetupUser(User user)
    {
        mockUsers
            .Setup(x => x.ListAsync(
                It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User> { user });
    }

    private static User SuccessfulUser(Result<User> result)
    {
        Assert.True(result.IsSuccess, result.Error);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Error);

        return Assert.IsType<User>(result.Value);
    }

    private void AssertLoginFailure(Result<User> result)
    {
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Contains("email або пароль", result.Error);
        Assert.Null(userSession.CurrentUser);

        mockLogger.Verify(
            x => x.Information(
                It.Is<string>(message =>
                    message.Contains("Login.ValidationFailed")
                    && message.Contains("email або пароль")),
                It.IsAny<int?>()),
            Times.Once);

        mockLogger.Verify(
            x => x.Error(
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<int?>()),
            Times.Never);
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

        return $"210000:{Convert.ToBase64String(salt)}"
            + $":{Convert.ToBase64String(hash)}";
    }
}

public class AuthServiceLogoutTests
{
    private readonly Mock<IAppLogger> mockLogger;
    private readonly UserSession userSession;
    private readonly AuthService authService;

    public AuthServiceLogoutTests()
    {
        var mockUsers = new Mock<IRepository<User>>();
        mockLogger = new Mock<IAppLogger>();
        userSession = new UserSession();

        var operations = new ServiceOperations(
            mockLogger.Object,
            userSession);

        authService = new AuthService(
            mockUsers.Object,
            userSession,
            operations);
    }

    [Fact]
    public async Task Logout_WithAuthenticatedUser_ClearsSession()
    {
        var user = new User
        {
            UserId = 1,
            Email = "user@example.com",
            FirstName = "John"
        };

        userSession.SignIn(user);

        Result result = await authService.LogoutAsync();

        Assert.True(result.IsSuccess, result.Error);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Error);
        Assert.Null(userSession.CurrentUser);

        mockLogger.Verify(
            x => x.Information("Logout", user.UserId),
            Times.Once);
    }

    [Fact]
    public async Task Logout_WithoutAuthenticatedUser_ReturnsFailure()
    {
        Result result = await authService.LogoutAsync();

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Contains("увійдіть у систему", result.Error);
        Assert.Null(userSession.CurrentUser);

        mockLogger.Verify(
            x => x.Information(
                It.Is<string>(message =>
                    message.Contains("Logout.ValidationFailed")
                    && message.Contains("увійдіть у систему")),
                It.IsAny<int?>()),
            Times.Once);

        mockLogger.Verify(
            x => x.Error(
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<int?>()),
            Times.Never);
    }
}