using System.Collections.ObjectModel;
using SmartTrip.Application.Services;
using SmartTrip.Domain.Entities;
using Smart_tripp.WPF.Commands;

namespace Smart_tripp.WPF.ViewModels;

public sealed class PackingTemplateViewModel : ViewModelBase
{
    private readonly Trip trip;
    private readonly PackingService packing;
    private readonly PackingTemplateService templateService;
    private readonly IToolHost host;
    private PackingTemplate? selectedTemplate;

    public PackingTemplateViewModel(Trip trip, PackingService packing,
        PackingTemplateService templateService, IToolHost host)
    {
        this.trip = trip;
        this.packing = packing;
        this.templateService = templateService;
        this.host = host;
        ApplyCommand = new AsyncCommand(
            () => host.RunAsync("Packing.ApplyTemplate", ApplyAsync), () => SelectedTemplate is not null);
        SelectedTemplate = Templates[0];
    }

    public IReadOnlyList<PackingTemplate> Templates { get; } = PackingTemplateService.Templates;

    public ObservableCollection<string> CurrentItems { get; } = [];

    public AsyncCommand ApplyCommand { get; }

    public PackingTemplate? SelectedTemplate
    {
        get => selectedTemplate;
        set
        {
            if (SetProperty(ref selectedTemplate, value))
            {
                ApplyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task<bool> LoadAsync()
    {
        var result = await packing.ListAsync(trip.TripId);
        if (result.IsFailure)
        {
            Status = result.Error;
            return false;
        }

        CurrentItems.Clear();
        foreach (PackingItem item in result.Value!)
        {
            CurrentItems.Add(item.Name);
        }

        return true;
    }

    private async Task ApplyAsync()
    {
        Status = string.Empty;
        if (SelectedTemplate is not { } template)
        {
            return;
        }

        var result = await templateService.ApplyAsync(trip.TripId, template.Name);
        if (result.IsFailure)
        {
            Status = result.Error;
            return;
        }

        await host.RefreshAsync();
        Status = $"Додано речей: {result.Value}.";
    }
}
