using System.Collections.ObjectModel;
using System.Globalization;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using Smart_tripp.WPF.Commands;

namespace Smart_tripp.WPF.ViewModels;

public sealed class RescheduleVisitViewModel : ViewModelBase
{
    private readonly Trip trip;
    private readonly ItineraryService itinerary;
    private readonly VisitRescheduleService rescheduling;
    private readonly IToolHost host;
    private ItineraryVisit? selectedVisit;
    private DateTime? date;
    private string time = string.Empty;
    private string notes = string.Empty;

    public RescheduleVisitViewModel(Trip trip, ItineraryService itinerary,
        VisitRescheduleService rescheduling, IToolHost host)
    {
        this.trip = trip;
        this.itinerary = itinerary;
        this.rescheduling = rescheduling;
        this.host = host;
        RescheduleCommand = new AsyncCommand(
            () => host.RunAsync("Itinerary.Reschedule", RescheduleAsync), () => SelectedVisit is not null);
    }

    public ObservableCollection<ItineraryVisit> Visits { get; } = [];

    public AsyncCommand RescheduleCommand { get; }

    public DateTime TripStart => trip.StartDate.Date;

    public DateTime TripEnd => trip.EndDate.Date;

    public ItineraryVisit? SelectedVisit
    {
        get => selectedVisit;
        set
        {
            if (!SetProperty(ref selectedVisit, value))
            {
                return;
            }

            Date = value?.VisitAt.Date;
            Time = value?.VisitAt.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
            Notes = value?.Notes ?? string.Empty;
            RescheduleCommand.RaiseCanExecuteChanged();
        }
    }

    public DateTime? Date
    {
        get => date;
        set => SetProperty(ref date, value);
    }

    public string Time
    {
        get => time;
        set => SetProperty(ref time, value);
    }

    public string Notes
    {
        get => notes;
        set => SetProperty(ref notes, value);
    }

    public async Task<bool> LoadAsync()
    {
        var result = await itinerary.ListAsync(trip.TripId);
        if (result.IsFailure)
        {
            Status = result.Error;
            return false;
        }

        SelectedVisit = null;
        Visits.Clear();
        foreach (ItineraryVisit visit in result.Value!)
        {
            Visits.Add(visit);
        }

        return true;
    }

    private async Task RescheduleAsync()
    {
        Status = string.Empty;
        if (SelectedVisit is not { } visit)
        {
            return;
        }

        if (Date is not { } day)
        {
            host.Reject("Itinerary.Reschedule", "Виберіть дату.");
            return;
        }

        if (!TimeSpan.TryParseExact(Time.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out TimeSpan clock))
        {
            host.Reject("Itinerary.Reschedule", "Введіть час у форматі ГГ:ХХ, наприклад 14:30.");
            return;
        }

        var result = await rescheduling.RescheduleAsync(visit.ItineraryVisitId, day.Date + clock, Notes);
        if (result.IsFailure)
        {
            Status = result.Error;
            return;
        }

        await host.RefreshAsync();
        Status = "Відвідування перенесено.";
    }
}
