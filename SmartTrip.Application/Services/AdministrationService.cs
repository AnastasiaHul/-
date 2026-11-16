using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class AdministrationService(
    UserSession session, IAdministrationRepository repository, IAppLogger logger)
{
    public const string ApprovePlace = "Admin.Place.Approve";
    public const string ReturnPlace = "Admin.Place.Return";
    public const string BlockAccount = "Admin.Account.Block";
    public const string UnblockAccount = "Admin.Account.Unblock";
    public const string CancelExcursion = "Admin.Excursion.Cancel";
    public const string ViewAudit = "Admin.Audit.View";

    public Task<Result<IReadOnlyList<PendingPlace>>> ListPendingAsync() =>
        RunAsync<IReadOnlyList<PendingPlace>>("Admin.Place.List", async _ =>
            Result<IReadOnlyList<PendingPlace>>.Success(await repository.ListPendingAsync().ConfigureAwait(false)));

    public Task<Result<AccountSummary>> FindAccountAsync(string? email) =>
        RunAsync<AccountSummary>("Admin.Account.Find", async _ =>
        {
            var address = AuthService.NormalizeEmail(email);
            if (address.IsFailure)
            {
                return new Failure(address.Error);
            }

            var account = await repository.FindAccountAsync(address.Value!).ConfigureAwait(false);
            return account is null ? Result<AccountSummary>.Failure("Акаунт не знайдено.") : account;
        });

    public Task<Result<ExcursionSummary>> FindExcursionAsync(string? code) =>
        RunAsync<ExcursionSummary>("Admin.Excursion.Find", async _ =>
        {
            var key = ServiceOperations.TryRequired(code, "Код бронювання", 80);
            if (key.IsFailure)
            {
                return new Failure(key.Error);
            }

            var booking = await repository.FindExcursionAsync(key.Value!.ToUpperInvariant()).ConfigureAwait(false);
            return booking is null ? Result<ExcursionSummary>.Failure("Бронювання не знайдено.") : booking;
        });

    public Task<Result<int>> ReviewPlaceAsync(int placeId, bool approve, string? reason) =>
        ChangeAsync(approve ? ApprovePlace : ReturnPlace, placeId, reason,
            (text, actor) => repository.ReviewPlaceAsync(placeId, approve, text, actor));

    public Task<Result<int>> SetAccountBlockedAsync(int userId, bool block, string? reason) =>
        ChangeAsync(block ? BlockAccount : UnblockAccount, userId, reason,
            (text, actor) => repository.SetAccountBlockedAsync(userId, block, text, actor));

    public Task<Result<int>> CancelExcursionAsync(int reservationId, string? reason) =>
        ChangeAsync(CancelExcursion, reservationId, reason,
            (text, actor) => repository.CancelExcursionAsync(reservationId, text, actor));

    public Task<Result<IReadOnlyList<AdminAuditEntry>>> ListAuditAsync(
        DateTime? fromLocal, DateTime? toLocal, string? action) =>
        RunAsync<IReadOnlyList<AdminAuditEntry>>(ViewAudit, async _ =>
        {
            if (fromLocal.HasValue && toLocal.HasValue && fromLocal.Value.Date > toLocal.Value.Date
                || toLocal?.Date == DateTime.MaxValue.Date)
            {
                return new Failure("Перевірте діапазон дат.");
            }

            var fromUtc = fromLocal?.Date.ToUniversalTime();
            var untilUtc = toLocal?.Date.AddDays(1).ToUniversalTime();
            var entries = await repository.ListAuditAsync(fromUtc, untilUtc, action).ConfigureAwait(false);
            return Result<IReadOnlyList<AdminAuditEntry>>.Success(entries);
        });

    private Task<Result<int>> ChangeAsync(string action, int target, string? reason,
        Func<string, int, Task<Result>> apply) =>
        RunAsync<int>(action, async actor =>
        {
            var validReason = ServiceOperations.TryRequired(reason, "Причина", 300);
            if (target <= 0 || validReason.IsFailure)
            {
                return new Failure(target <= 0 ? "Виберіть запис." : validReason.Error);
            }

            Result result = await apply(validReason.Value!, actor).ConfigureAwait(false);
            return result.IsFailure ? Result<int>.Failure(result.Error) : target;
        }, target);

    private async Task<Result<T>> RunAsync<T>(string action, Func<int, Task<Result<T>>> run, int? target = null)
    {
        int? actor = session.CurrentUser?.UserId;
        string context = target.HasValue ? $": target {target}" : string.Empty;
        var access = session.TryRequireRole("Admin");
        if (access.IsFailure)
        {
            logger.Information($"{action}.ValidationFailed{context}: access denied", actor);
            return new Failure("Ця дія доступна лише адміністратору.");
        }

        Result<T> result = await run(access.Value!.UserId).ConfigureAwait(false);
        logger.Information(result.IsSuccess
            ? $"{action}.Succeeded{context}" : $"{action}.ValidationFailed{context}: {result.Error}", actor);
        return result;
    }
}
