using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class PlaceService(
	IRepository<Place> places,
	UserSession session,
	ServiceOperations operations)
{
	public Task<IReadOnlyList<Place>> ListOwnAsync()
	{
		return operations.RunAsync("ListPartnerPlaces", () =>
		{
			int partnerId =
				session.RequireRole("Partner").UserId;

			return places.ListAsync(
				x => x.PartnerId == partnerId);
		});
	}

	public Task<Place> SaveAsync(
		int? id,
		string name,
		string category,
		decimal price,
		string address,
		string schedule,
		string description)
	{
		return operations.RunAsync(
			id is null ? "CreatePlace" : "EditPlace",
			async () =>
			{
				int partnerId =
					session.RequireRole("Partner").UserId;

				string validName =
					ServiceOperations.Required(
						name, "Назва", 200);

				string validCategory =
					ServiceOperations.Required(
						category, "Категорія", 100);

				decimal validPrice =
					ServiceOperations.Money(price);

				if (address.Length > 300
					|| schedule.Length > 300
					|| description.Length > 3000)
				{
					throw new ArgumentException(
						"Адреса та графік: максимум 300 символів. "
						+ "Опис: максимум 3000 символів.");
				}

				Place place = id is null
					? new Place
					{
						PartnerId = partnerId,
						Status = "Published"
					}
					: await OwnedAsync(id.Value)
						.ConfigureAwait(false);

				place.Name = validName;
				place.Category = validCategory;
				place.Price = validPrice;
				place.Address = address.Trim();
				place.Schedule = schedule.Trim();
				place.Description = description.Trim();

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

				return place;
			});
	}

	public Task DeleteAsync(int id)
	{
		return operations.RunAsync("DeletePlace", async () =>
		{
			Place place = await OwnedAsync(id)
				.ConfigureAwait(false);

			await places.DeleteAsync(place)
				.ConfigureAwait(false);
		});
	}

	private async Task<Place> OwnedAsync(int id)
	{
		int partnerId =
			session.RequireRole("Partner").UserId;

		Place place = await places.FindAsync(id)
			.ConfigureAwait(false)
			?? throw new ArgumentException(
				"Місце не знайдене.");

		if (place.PartnerId != partnerId)
		{
			throw new UnauthorizedAccessException(
				"Ви можете керувати лише власними місцями.");
		}

		return place;
	}
}
