using System.Globalization;
using System.Text;
using SmartTrip.Application.Common;
using SmartTrip.Application.Contracts;
using SmartTrip.Domain.Entities;

namespace SmartTrip.Application.Services;

public sealed class TripReportService(
    IRepository<ItineraryVisit> visits,
    IRepository<Expense> expenses,
    IRepository<PackingItem> packing,
    BookingManagementService bookings,
    TripService trips,
    ServiceOperations operations)
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("uk-UA");

    public Task<Result<string>> BuildHtmlAsync(int tripId)
    {
        return operations.RunResultAsync<string>("Trip.Report", async () =>
        {
            var owned = await trips.OwnedAsync(tripId).ConfigureAwait(false);
            if (owned.IsFailure)
            {
                return new Failure(owned.Error);
            }

            var booked = await bookings.ListAsync(tripId).ConfigureAwait(false);
            if (booked.IsFailure)
            {
                return new Failure(booked.Error);
            }

            Trip trip = owned.Value!;
            var plan = (await visits.ListAsync(x => x.TripId == tripId).ConfigureAwait(false))
                .OrderBy(x => x.VisitAt).ToList();
            var spent = (await expenses.ListAsync(x => x.TripId == tripId).ConfigureAwait(false))
                .OrderBy(x => x.Date).ToList();
            var things = (await packing.ListAsync(x => x.TripId == tripId).ConfigureAwait(false))
                .OrderBy(x => x.Name).ToList();
            decimal total = spent.Sum(x => x.Amount);

            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"uk\"><head><meta charset=\"utf-8\">")
                .Append("<title>Звіт про подорож</title><style>")
                .Append("body{font-family:Segoe UI,Arial,sans-serif;max-width:900px;margin:32px auto;padding:0 16px;color:#19383d}")
                .Append("h1,h2{color:#167d6e}table{border-collapse:collapse;width:100%}")
                .Append("th,td{border:1px solid #d6e1e3;padding:6px 10px;text-align:left;vertical-align:top}")
                .Append("th{background:#e8f1ee}</style></head><body>");
            html.Append("<h1>").Append(Encode(trip.Title)).Append("</h1><p>")
                .Append(Encode(trip.City)).Append(" · ")
                .Append(Day(trip.StartDate)).Append(" — ").Append(Day(trip.EndDate))
                .Append(" · учасників: ").Append(trip.PeopleCount).Append("</p>");
            if (trip.Interests.Length > 0)
            {
                html.Append("<p>Інтереси: ").Append(Encode(trip.Interests)).Append("</p>");
            }

            html.Append("<h2>Бюджет</h2><p>Заплановано: ").Append(Money(trip.Budget))
                .Append(" · Витрачено: ").Append(Money(total))
                .Append(" · Залишок: ").Append(Money(trip.Budget - total)).Append("</p>");

            Table(html, "План відвідувань", ["Дата і час", "Місце", "Примітка"],
                plan.Select(x => new[]
                {
                    x.VisitAt.ToString("dd.MM.yyyy HH:mm", Culture), x.PlaceName, x.Notes
                }));
            Table(html, "Бронювання", ["Тип", "Деталі", "Початок", "Код", "Сума", "Статус"],
                booked.Value!.Select(x => new[]
                {
                    x.KindName, x.Title, Day(x.StartsAt), x.Code, Money(x.Price), x.StatusName
                }));
            Table(html, "Витрати", ["Дата", "Категорія", "Опис", "Сума"],
                spent.Select(x => new[] { Day(x.Date), x.Category, x.Description, Money(x.Amount) }));
            Table(html, "Список речей", ["Річ", "Спаковано"],
                things.Select(x => new[] { x.Name, x.IsPacked ? "Так" : "Ні" }));
            html.Append("</body></html>");
            return html.ToString();
        });
    }

    private static void Table(StringBuilder html, string title, string[] headers,
        IEnumerable<string[]> rows)
    {
        html.Append("<h2>").Append(title).Append("</h2>");
        var list = rows.ToList();
        if (list.Count == 0)
        {
            html.Append("<p>Записів немає.</p>");
            return;
        }

        html.Append("<table><tr>");
        foreach (string header in headers)
        {
            html.Append("<th>").Append(header).Append("</th>");
        }

        html.Append("</tr>");
        foreach (string[] row in list)
        {
            html.Append("<tr>");
            foreach (string cell in row)
            {
                html.Append("<td>").Append(Encode(cell)).Append("</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</table>");
    }

    // Кодуємо лише службові символи HTML, щоб український текст залишався читабельним.
    private static string Encode(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string Day(DateTime value) => value.ToString("dd.MM.yyyy", Culture);

    private static string Money(decimal value) => value.ToString("N2", Culture) + " ₴";
}
