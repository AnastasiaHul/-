namespace SmartTrip.Domain.Entities;

public class Favorite
{
    public int FavoriteId { get; set; }

    public int UserId { get; set; }

    public int PlaceId { get; set; }
}