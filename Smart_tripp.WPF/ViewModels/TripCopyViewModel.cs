using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using Smart_tripp.WPF.Commands;

namespace Smart_tripp.WPF.ViewModels;

public sealed class TripCopyViewModel : ViewModelBase
{
    private readonly Trip trip;
    private readonly TripCopyService copies;
    private string title;
    private DateTime? startDate;

    public TripCopyViewModel(Trip trip, TripCopyService copies, IToolHost host)
    {
        this.trip = trip;
        this.copies = copies;
        title = trip.Title.Length > 140 ? trip.Title : trip.Title + " (копія)";
        startDate = DateTime.Today.AddDays(7);
        CopyCommand = new AsyncCommand(
            () => host.RunAsync("Trip.Copy", CopyAsync),
            () => !string.IsNullOrWhiteSpace(Title) && StartDate is not null);
    }

    public AsyncCommand CopyCommand { get; }

    public DateTime MinimumDate => DateTime.Today;

    public string Title
    {
        get => title;
        set
        {
            if (SetProperty(ref title, value))
            {
                CopyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DateTime? StartDate
    {
        get => startDate;
        set
        {
            if (SetProperty(ref startDate, value))
            {
                CopyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private async Task CopyAsync()
    {
        Status = string.Empty;
        if (StartDate is not { } start)
        {
            return;
        }

        var result = await copies.CopyAsync(trip.TripId, Title, start);
        if (result.IsFailure)
        {
            Status = result.Error;
            return;
        }

        TripCopyResult copy = result.Value!;
        string skipped = copy.SkippedVisits > 0
            ? $", пропущено недоступних місць — {copy.SkippedVisits}"
            : string.Empty;
        Status = $"Створено «{copy.Trip.Title}»: відвідувань — {copy.Visits}, речей — {copy.PackingItems}"
            + skipped + ". Вона з’явиться у «Моїх подорожах».";
    }
}
