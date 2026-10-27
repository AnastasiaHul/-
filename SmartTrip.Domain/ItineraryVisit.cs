using System;

namespace SmartTrip.Domain.Entities;

public sealed class ItineraryVisit
{
    public int ItineraryVisitId { get; set; }

    public int TripId { get; set; }

    public int PlaceId { get; set; }

    public string PlaceName { get; set; } = string.Empty;

    public DateTime VisitAt { get; set; }

    public string Notes { get; set; } = string.Empty;
}