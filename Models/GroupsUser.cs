using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class GroupsUser
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();

    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
}
