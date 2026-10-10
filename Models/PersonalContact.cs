using System;

namespace TODOLISTAPI.Models;

/// <summary>
/// A contact saved privately by one user. Unlike GroupMember, this record
/// is not attached to a group and therefore cannot be used for group task
/// forwarding or mentions.
/// </summary>
public partial class PersonalContact
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public DateTime AddedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
