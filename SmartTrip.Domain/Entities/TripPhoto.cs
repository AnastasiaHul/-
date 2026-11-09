namespace SmartTrip.Domain.Entities;

public sealed class TripPhoto
{
    public int TripPhotoId { get; set; }
    public int TripId { get; set; }
    public int? PlaceId { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string PlaceName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public byte[] Image { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
