namespace SmartTrip.Domain.Entities;

public class PackingItem
{
    public int PackingItemId { get; set; }

    public int TripId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsPacked { get; set; }
}