using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class SnoozeLog
{
    public int Id { get; set; }

    public int TaskId { get; set; }

    public int UserId { get; set; }

    public DateTime SnoozedAt { get; set; }

    public DateTime WakeAt { get; set; }

    public virtual Task Task { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
