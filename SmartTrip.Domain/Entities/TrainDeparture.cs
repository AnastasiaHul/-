using System;

namespace SmartTrip.Domain.Entities;

public sealed class TrainDeparture
{
    public int TrainDepartureId { get; set; }

    public string TrainNumber { get; set; } = string.Empty;

    public string DepartureCity { get; set; } = string.Empty;

    public string ArrivalCity { get; set; } = string.Empty;

    public DateTime DepartureAt { get; set; }

    public DateTime ArrivalAt { get; set; }

    public int WagonCount { get; set; }

    public int SeatsPerWagon { get; set; }

    public decimal TicketPrice { get; set; }
}