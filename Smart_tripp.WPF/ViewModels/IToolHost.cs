namespace Smart_tripp.WPF.ViewModels;

// Те, що ViewModel потрібно від вікна. Завдяки цьому ViewModel не залежить від елементів WPF.
public interface IToolHost
{
    // Виконує дію з блокуванням форми та записом помилок у журнал.
    Task RunAsync(string action, Func<Task> work);

    // Показує та записує в журнал помилку введення.
    void Reject(string action, string message);

    bool Confirm(string message);

    // Оновлює дані всіх вкладок.
    Task RefreshAsync();
}
