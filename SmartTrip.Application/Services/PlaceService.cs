using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class PlaceService(
    IRepository<Place> places,
    UserSession session,
    ServiceOperations operations)
{
    public Task<Result<IReadOnlyList<Place>>> ListOwnAsync()
    {
        return operations.RunResultAsync<IReadOnlyList<Place>>(
            "ListPartnerPlaces",
            async () =>
            {
                Result<int> partner = GetPartnerId();

                if (partner.IsFailure)
                {
                    return Result<IReadOnlyList<Place>>.Failure(
                        partner.Error);
                }

                int partnerId = partner.Value;

                IReadOnlyList<Place> result =
                    await places.ListAsync(
                        x => x.PartnerId == partnerId)
                    .ConfigureAwait(false);

                return Result<IReadOnlyList<Place>>.Success(result);
            });
    }

    public Task<Result<Place>> SaveAsync(
        int? id,
        string? name,
        string? category,
        decimal price,
        string? address,
        string? schedule,
        string? description)
    {
        return operations.RunResultAsync<Place>(
            id is null ? "CreatePlace" : "EditPlace",
            async () =>
            {
                Result<int> partner = GetPartnerId();

                if (partner.IsFailure)
                {
                    return Result<Place>.Failure(partner.Error);
                }

                Result<string> validName =
                    ServiceOperations.TryRequired(
                        name,
                        "Назва",
                        200);

                if (validName.IsFailure)
                {
                    return Result<Place>.Failure(validName.Error);
                }

                Result<string> validCategory =
                    ServiceOperations.TryRequired(
                        category,
                        "Категорія",
                        100);

                if (validCategory.IsFailure)
                {
                    return Result<Place>.Failure(
                        validCategory.Error);
                }

                Result<decimal> validPrice =
                    ServiceOperations.TryMoney(price);

                if (validPrice.IsFailure)
                {
                    return Result<Place>.Failure(validPrice.Error);
                }

                string addressText = address ?? string.Empty;
                string scheduleText = schedule ?? string.Empty;
                string descriptionText =
                    description ?? string.Empty;

                if (addressText.Length > 300
                    || scheduleText.Length > 300
                    || descriptionText.Length > 3000)
                {
                    return Result<Place>.Failure(
                        "Адреса та графік: максимум 300 символів. "
                        + "Опис: максимум 3000 символів.");
                }

                int partnerId = partner.Value;
                Place place;

                if (id is null)
                {
                    place = new Place
                    {
                        PartnerId = partnerId,
                        Status = "Published"
                    };
                }
                else
                {
                    Result<Place> owned =
                        await OwnedAsync(id.Value, partnerId)
                            .ConfigureAwait(false);

                    if (owned.IsFailure)
                    {
                        return Result<Place>.Failure(owned.Error);
                    }

                    place = owned.Value!;
                }

                place.Name = validName.Value!;
                place.Category = validCategory.Value!;
                place.Price = validPrice.Value;
                place.Address = addressText.Trim();
                place.Schedule = scheduleText.Trim();
                place.Description = descriptionText.Trim();

                if (id is null)
                {
                    await places.AddAsync(place)
                        .ConfigureAwait(false);
                }
                else
                {
                    await places.UpdateAsync(place)
                        .ConfigureAwait(false);
                }

                return Result<Place>.Success(place);
            });
    }

    public Task<Result> DeleteAsync(int id)
    {
        return operations.RunResultAsync(
            "DeletePlace",
            async () =>
            {
                Result<int> partner = GetPartnerId();

                if (partner.IsFailure)
                {
                    return Result.Failure(partner.Error);
                }

                Result<Place> owned =
                    await OwnedAsync(id, partner.Value)
                        .ConfigureAwait(false);

                if (owned.IsFailure)
                {
                    return Result.Failure(owned.Error);
                }

                await places.DeleteAsync(owned.Value!)
                    .ConfigureAwait(false);

                return Result.Success();
            });
    }

    private Result<int> GetPartnerId()
    {
        User? user = session.CurrentUser;

        if (user is null)
        {
            return Result<int>.Failure(
                "Спочатку увійдіть у систему.");
        }

        if (user.Role != "Partner")
        {
            return Result<int>.Failure(
                "Керування місцями доступне лише партнеру.");
        }

        return Result<int>.Success(user.UserId);
    }

    private async Task<Result<Place>> OwnedAsync(
        int id,
        int partnerId)
    {
        if (id <= 0)
        {
            return Result<Place>.Failure(
                "Некоректний ідентифікатор місця.");
        }

        Place? place = await places.FindAsync(id)
            .ConfigureAwait(false);

        if (place is null)
        {
            return Result<Place>.Failure(
                "Місце не знайдене.");
        }

        if (place.PartnerId != partnerId)
        {
            return Result<Place>.Failure(
                "Ви можете керувати лише власними місцями.");
        }

        return Result<Place>.Success(place);
    }
}