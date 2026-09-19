using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class Task
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsTimeBased { get; set; }

    public DateOnly? DueDate { get; set; }

    public TimeOnly? DueTime { get; set; }

    public int? GroupId { get; set; }

    public int CreatedBy { get; set; }

    public int? AssignedTo { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int GeofenceRadiusMeters { get; set; }

    public bool GeofenceEnabled { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<ForwardedTask> ForwardedTasks { get; set; } = new List<ForwardedTask>();

    public virtual GroupsUser? Group { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<SnoozeLog> SnoozeLogs { get; set; } = new List<SnoozeLog>();

    public virtual ICollection<TaskMention> TaskMentions { get; set; } = new List<TaskMention>();

    public virtual ICollection<TaskResponse> TaskResponses { get; set; } = new List<TaskResponse>();
}
