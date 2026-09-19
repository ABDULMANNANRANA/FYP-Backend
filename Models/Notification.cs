using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class Notification
{
    public int Id { get; set; }

    public int TaskId { get; set; }

    public int UserId { get; set; }

    public string? Message { get; set; }

    public bool IsRead { get; set; }

    public DateTime SentAt { get; set; }

    public virtual Task Task { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
