using System;

namespace TODOLISTAPI.Models;

public partial class PersonalContact
{
    public int Id { get; set; }

    public int UserId { get; set; }

    // Null means a grandfathered contact from before the request workflow.
    public int? ContactUserId { get; set; }

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public DateTime AddedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
