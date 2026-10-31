namespace SmartTrip.Domain.Entities;

public sealed class ExcursionSession
{
    public int ExcursionSessionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string MeetingAddress { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }

    public int DurationMinutes { get; set; }

    public int Capacity { get; set; }

    public decimal PricePerPerson { get; set; }

    public bool IsActive { get; set; } = true;
}