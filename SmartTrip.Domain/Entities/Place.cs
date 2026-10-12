using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartTrip.Domain.Entities
{
    public class Place
    {
        public int PlaceId { get; set; }
        public int? PartnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Address { get; set; }
        public string? Schedule { get; set; }
        public string Status { get; set; } = "OnReview";
    }
}
