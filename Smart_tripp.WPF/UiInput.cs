using System.Globalization;
using System.Windows.Controls;
using SmartTrip.Application.Common;

namespace Smart_tripp.WPF;
internal static class UiInput
{
    public static Result<decimal> Money(TextBox box)
    {
        string value = box.Text.Trim().Replace(',', '.');
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal amount))
        {
            return new Failure("Введіть суму числом, наприклад 1500,50.");
        }

        return amount;
    }

    public static Result<int> Integer(TextBox box)
    {
        if (!int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            return new Failure("Введіть кількість людей цілим числом.");
        }

        return result;
    }

    public static Result<DateTime> Date(DatePicker picker)
    {
        var value1 = picker.SelectedDate;
        if (value1 is null)
        {
            return new Failure("Виберіть дату.");
        }

        return value1.Value;
    }

    public static Result<T> Selected<T>(DataGrid grid)
        where T : class
    {
        var value2 = grid.SelectedItem as T;
        if (value2 is null)
        {
            return new Failure("Спочатку виберіть рядок у таблиці.");
        }

        return value2;
    }

    public static string Action(object sender)
    {
        return ((Button)sender).Tag?.ToString() ?? string.Empty;
    }
}
