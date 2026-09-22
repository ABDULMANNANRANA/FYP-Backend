using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class User
{
    public int Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string Password { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public bool? IsActive { get; set; }

    public double? LastLatitude { get; set; }

    public double? LastLongitude { get; set; }

    public DateTime? LastLocationUpdatedAt { get; set; }

    public virtual ICollection<ForwardedTask> ForwardedTaskForwardedByNavigations { get; set; } = new List<ForwardedTask>();

    public virtual ICollection<ForwardedTask> ForwardedTaskForwardedToNavigations { get; set; } = new List<ForwardedTask>();

    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();

    public virtual ICollection<GroupsUser> GroupsUsers { get; set; } = new List<GroupsUser>();

    public virtual ICollection<Notification> NotificationSenders { get; set; } = new List<Notification>();

    public virtual ICollection<Notification> NotificationUsers { get; set; } = new List<Notification>();

    public virtual ICollection<SnoozeLog> SnoozeLogs { get; set; } = new List<SnoozeLog>();

    public virtual ICollection<Task> TaskAssignedToNavigations { get; set; } = new List<Task>();

    public virtual ICollection<Task> TaskCreatedByNavigations { get; set; } = new List<Task>();

    public virtual ICollection<TaskMention> TaskMentionMentionedByNavigations { get; set; } = new List<TaskMention>();

    public virtual ICollection<TaskMention> TaskMentionMentionedUsers { get; set; } = new List<TaskMention>();

    public virtual ICollection<TaskResponse> TaskResponses { get; set; } = new List<TaskResponse>();

    public virtual UserLocation? UserLocation { get; set; }

    public virtual UserSetting? UserSetting { get; set; }
}
