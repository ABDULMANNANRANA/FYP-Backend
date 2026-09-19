using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class GroupMember
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public int? UserId { get; set; }

    public string? Name { get; set; }

    public string? Phone { get; set; }

    public string Role { get; set; } = null!;

    public DateTime AddedAt { get; set; }

    public virtual GroupsUser Group { get; set; } = null!;

    public virtual User? User { get; set; }
}
