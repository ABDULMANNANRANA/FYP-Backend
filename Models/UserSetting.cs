using System;
using System.Collections.Generic;

namespace TODOLISTAPI.Models;

public partial class UserSetting
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public bool NotificationEnabled { get; set; }

    public bool DarkModeEnabled { get; set; }

    public int SnoozeMinutes { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
