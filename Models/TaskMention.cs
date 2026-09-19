using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class TaskMention
{
    public int Id { get; set; }

    public int TaskId { get; set; }

    public int MentionedUserId { get; set; }

    public int MentionedBy { get; set; }

    public DateTime MentionedAt { get; set; }

    public virtual User MentionedByNavigation { get; set; } = null!;

    public virtual User MentionedUser { get; set; } = null!;

    public virtual Task Task { get; set; } = null!;
}
