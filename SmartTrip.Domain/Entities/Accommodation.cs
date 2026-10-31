namespace SmartTrip.Domain.Entities;

public sealed class Accommodation
{
    public int AccommodationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int MaxGuests { get; set; }

    public decimal PricePerNight { get; set; }

    public bool IsActive { get; set; } = true;
}