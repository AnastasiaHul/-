using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed record PackingTemplate(string Name, IReadOnlyList<string> Items);

public sealed class PackingTemplateService(
    IRepository<PackingItem> items,
    TripService trips,
    ServiceOperations operations)
{
    public static IReadOnlyList<PackingTemplate> Templates { get; } =
    [
        new("Базовий", ["Паспорт", "Гроші та банківські картки", "Телефон і зарядний пристрій",
            "Павербанк", "Аптечка", "Зубна щітка та паста", "Змінний одяг"]),
        new("Пляжний відпочинок", ["Купальний костюм", "Сонцезахисний крем", "Сонячні окуляри",
            "Пляжний рушник", "Головний убір", "Шльопанці"]),
        new("Гори та походи", ["Трекінгове взуття", "Рюкзак", "Дощовик", "Теплий шар одягу",
            "Ліхтарик", "Фляга для води", "Пластирі"]),
        new("Міська прогулянка", ["Зручне взуття", "Парасолька", "Маленький рюкзак",
            "Пляшка води", "Навушники"]),
        new("Відрядження", ["Ноутбук", "Зарядка для ноутбука", "Діловий одяг", "Документи",
            "Блокнот і ручка"])
    ];

    // Додає лише ті речі шаблону, яких ще немає у списку. Повертає кількість доданих.
    public Task<Result<int>> ApplyAsync(int tripId, string templateName)
    {
        return operations.RunResultAsync<int>("Packing.ApplyTemplate", async () =>
        {
            var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (owned.IsFailure)
            {
                return new Failure(owned.Error);
            }

            PackingTemplate? template = Templates.FirstOrDefault(x => x.Name == templateName);
            if (template is null)
            {
                return new Failure("Виберіть шаблон списку.");
            }

            var existing = (await items.ListAsync(x => x.TripId == tripId).ConfigureAwait(false))
                .Select(x => x.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            int added = 0;
            foreach (string name in template.Items.Where(x => !existing.Contains(x)))
            {
                await items.AddAsync(new PackingItem { TripId = tripId, Name = name }).ConfigureAwait(false);
                added++;
            }

            return added;
        });
    }
}
