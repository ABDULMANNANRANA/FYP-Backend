using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class ForwardedTask
{
    public int Id { get; set; }

    public int OriginalTaskId { get; set; }

    public int ForwardedBy { get; set; }

    public int ForwardedTo { get; set; }

    public DateTime ForwardedAt { get; set; }

    public virtual User ForwardedByNavigation { get; set; } = null!;

    public virtual User ForwardedToNavigation { get; set; } = null!;

    public virtual Task OriginalTask { get; set; } = null!;
}
