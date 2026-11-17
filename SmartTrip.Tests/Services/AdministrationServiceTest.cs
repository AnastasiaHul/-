using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using SmartTrip.Application.Common;

namespace SmartTrip.Tests.Services;

public class AdministrationServiceTests
{
    private const string AccessDenied = "Ця дія доступна лише адміністратору.";
    private const string SelectRecord = "Виберіть запис.";
    private const string InvalidDateRange = "Перевірте діапазон дат.";
    private const string ReasonKeyword = "Причина";

    private readonly Mock<IAdministrationRepository> _repo = new();
    private readonly UserSession _session = new();
    private readonly AdministrationService _service;

    public AdministrationServiceTests()
    {
        _service = new AdministrationService(_session, _repo.Object, new Mock<IAppLogger>().Object);
    }

    private void SetUser(string role)
    {
        _session.SignIn(new User
        {
            UserId = 1,
            Email = $"{role.ToLower()}@smarttrip.com",
            Role = role
        });
    }

    private Func<Task<(bool Failed, string? Error)>>[] AllAdminActions() =>
    [
        async () => { var r = await _service.ReviewPlaceAsync(10, true, "reason"); return (r.IsFailure, r.Error); },
        async () => { var r = await _service.SetAccountBlockedAsync(5, true, "reason"); return (r.IsFailure, r.Error); },
        async () => { var r = await _service.CancelExcursionAsync(3, "reason"); return (r.IsFailure, r.Error); },
        async () => { var r = await _service.ListAuditAsync(null, null, null); return (r.IsFailure, r.Error); },
    ];

    private void VerifyRepositoryNeverCalled() =>
        _repo.VerifyNoOtherCalls();

    [Fact]
    public async Task AllActions_WhenUserIsNotAdmin_ReturnAccessDenied_AndDoNotCallRepository()
    {
        SetUser("User");

        foreach (var action in AllAdminActions())
        {
            var (failed, error) = await action();
            Assert.True(failed);
            Assert.Equal(AccessDenied, error);
        }
        VerifyRepositoryNeverCalled();
    }

    [Fact]
    public async Task AllActions_WhenNoCurrentUser_ReturnFailure_AndDoNotCallRepository()
    {
        foreach (var action in AllAdminActions())
        {
            var (failed, _) = await action();
            Assert.True(failed);
        }
        VerifyRepositoryNeverCalled();
    }

    [Theory]
    [InlineData(true, "Approved by admin")]
    [InlineData(false, "Needs correction")]
    public async Task ReviewPlaceAsync_WhenValidAdmin_ReturnsSuccess(bool approve, string reason)
    {
        SetUser("Admin");
        _repo.Setup(r => r.ReviewPlaceAsync(10, approve, reason, 1)).ReturnsAsync(Result.Success());

        var result = await _service.ReviewPlaceAsync(10, approve, reason);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
        _repo.Verify(r => r.ReviewPlaceAsync(10, approve, reason, 1), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ReviewPlaceAsync_WhenReasonMissing_ReturnsError(string? reason)
    {
        SetUser("Admin");

        var result = await _service.ReviewPlaceAsync(10, false, reason);

        Assert.True(result.IsFailure);
        Assert.Contains(ReasonKeyword, result.Error);
        VerifyRepositoryNeverCalled();
    }

    [Fact]
    public async Task ReviewPlaceAsync_WhenRepositoryFails_ReturnsFailure()
    {
        SetUser("Admin");
        _repo.Setup(r => r.ReviewPlaceAsync(10, true, "Valid reason", 1))
             .ReturnsAsync(Result.Failure("Place not found"));

        var result = await _service.ReviewPlaceAsync(10, true, "Valid reason");

        Assert.True(result.IsFailure);
        Assert.Contains("Place not found", result.Error);
    }

    [Theory]
    [InlineData(true, "Spam detected")]
    [InlineData(false, "Issue resolved")]
    public async Task SetAccountBlockedAsync_WhenValidAdmin_ReturnsSuccess(bool block, string reason)
    {
        SetUser("Admin");
        _repo.Setup(r => r.SetAccountBlockedAsync(5, block, reason, 1)).ReturnsAsync(Result.Success());

        var result = await _service.SetAccountBlockedAsync(5, block, reason);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
        _repo.Verify(r => r.SetAccountBlockedAsync(5, block, reason, 1), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SetAccountBlockedAsync_WhenUserIdInvalid_ReturnsError(int userId)
    {
        SetUser("Admin");

        var result = await _service.SetAccountBlockedAsync(userId, true, "Valid reason");

        Assert.True(result.IsFailure);
        Assert.Equal(SelectRecord, result.Error);
        VerifyRepositoryNeverCalled();
    }

    [Fact]
    public async Task SetAccountBlockedAsync_WhenRepositoryFails_ReturnsFailure()
    {
        SetUser("Admin");
        _repo.Setup(r => r.SetAccountBlockedAsync(5, true, "Spam detected", 1))
             .ReturnsAsync(Result.Failure("User not found"));

        var result = await _service.SetAccountBlockedAsync(5, true, "Spam detected");

        Assert.True(result.IsFailure);
        Assert.Contains("User not found", result.Error);
    }

    [Theory]
    [InlineData("Weather conditions")]
    [InlineData("Guide illness")]
    public async Task CancelExcursionAsync_WhenValidAdmin_ReturnsSuccess(string reason)
    {
        SetUser("Admin");
        _repo.Setup(r => r.CancelExcursionAsync(3, reason, 1)).ReturnsAsync(Result.Success());

        var result = await _service.CancelExcursionAsync(3, reason);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value);
        _repo.Verify(r => r.CancelExcursionAsync(3, reason, 1), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CancelExcursionAsync_WhenReasonMissing_ReturnsError(string? reason)
    {
        SetUser("Admin");

        var result = await _service.CancelExcursionAsync(3, reason);

        Assert.True(result.IsFailure);
        Assert.Contains(ReasonKeyword, result.Error);
        VerifyRepositoryNeverCalled();
    }

    [Fact]
    public async Task CancelExcursionAsync_RepositoryFails_ReturnsFailure()
    {
        SetUser("Admin");
        _repo.Setup(r => r.CancelExcursionAsync(3, "Weather conditions", 1))
             .ReturnsAsync(Result.Failure("Reservation already cancelled"));

        var result = await _service.CancelExcursionAsync(3, "Weather conditions");

        Assert.True(result.IsFailure);
        Assert.Contains("Reservation already cancelled", result.Error);
    }

    [Fact]
    public async Task ListAuditAsync_WhenValidParams_ReturnsEntries()
    {
        SetUser("Admin");
        var entries = new List<AdminAuditEntry>
        {
            new() { AdminAuditEntryId = 1, ActorId = 1, Action = AdministrationService.BlockAccount,
                    TargetId = 5, Reason = "Rule violation", OccurredAtUtc = DateTime.UtcNow }
        };
        _repo.Setup(r => r.ListAuditAsync(null, null, AdministrationService.BlockAccount)).ReturnsAsync(entries);

        var result = await _service.ListAuditAsync(null, null, AdministrationService.BlockAccount);

        Assert.True(result.IsSuccess);
        var list = Assert.IsAssignableFrom<IEnumerable<AdminAuditEntry>>(result.Value);
        Assert.Single(list);
        _repo.Verify(r => r.ListAuditAsync(null, null, AdministrationService.BlockAccount), Times.Once);
    }

    [Fact]
    public async Task ListAuditAsync_WhenRepositoryReturnsEmpty_ReturnsSuccessEmpty()
    {
        SetUser("Admin");
        _repo.Setup(r => r.ListAuditAsync(null, null, null)).ReturnsAsync(new List<AdminAuditEntry>());

        var result = await _service.ListAuditAsync(null, null, null);

        Assert.True(result.IsSuccess);
        var list = Assert.IsAssignableFrom<IEnumerable<AdminAuditEntry>>(result.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task ListAuditAsync_WhenDateRangeInvalid_ReturnsError()
    {
        SetUser("Admin");
        var from = new DateTime(2026, 11, 10);
        var to = new DateTime(2026, 11, 1);

        var result = await _service.ListAuditAsync(from, to, null);

        Assert.True(result.IsFailure);
        Assert.Equal(InvalidDateRange, result.Error);
        VerifyRepositoryNeverCalled();
    }
}