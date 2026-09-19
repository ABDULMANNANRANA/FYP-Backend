using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class TaskResponse
{
    public int Id { get; set; }

    public int TaskId { get; set; }

    public int UserId { get; set; }

    public string Response { get; set; } = null!;

    public DateTime RespondedAt { get; set; }

    public virtual Task Task { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
