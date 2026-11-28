using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Smart_tripp.WPF.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    private string status = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    // Повідомлення про результат останньої дії.
    public string Status
    {
        get => status;
        protected set => SetProperty(ref status, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
