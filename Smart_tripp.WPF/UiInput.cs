using System.Globalization;
using System.Windows.Controls;

namespace Smart_tripp.WPF;

internal static class UiInput
{
    public static decimal Money(TextBox box)
    {
        string value = box.Text.Trim().Replace(',', '.');

        if (!decimal.TryParse(
            value,
            NumberStyles.AllowDecimalPoint
                | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out decimal amount))
        {
            throw new ArgumentException(
                "Введіть суму числом, наприклад 1500,50.");
        }

        return amount;
    }

    public static int Integer(TextBox box)
    {
        if (!int.TryParse(
            box.Text.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int result))
        {
            throw new ArgumentException(
                "Введіть кількість людей цілим числом.");
        }

        return result;
    }

    public static DateTime Date(DatePicker picker)
    {
        return picker.SelectedDate
            ?? throw new ArgumentException(
                "Виберіть дату.");
    }

    public static T Selected<T>(DataGrid grid)
        where T : class
    {
        return grid.SelectedItem as T
            ?? throw new ArgumentException(
                "Спочатку виберіть рядок у таблиці.");
    }

    public static string Action(object sender)
    {
        return ((Button)sender).Tag?.ToString()
            ?? string.Empty;
    }
}