using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class UserLocation
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public decimal? Accuracy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
