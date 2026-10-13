namespace SmartTrip.Domain.Entities;

public class Review
{
    public int ReviewId { get; set; }

    public int UserId { get; set; }

    public int PlaceId { get; set; }

    public int Rating { get; set; }

    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}