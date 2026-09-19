using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TODOLISTAPI.Models;

namespace TODOLISTAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly TodoSmartAlertsContext _context;

        public UserController(TodoSmartAlertsContext context)
        {
            _context = context;
        }

        // ============================================================
        // Helper Method
        // Get Logged-In User ID from JWT
        // ============================================================
        private bool TryGetUserId(out int userId)
        {
            userId = 0;

            // First try standard JWT NameIdentifier claim
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // If not found, try custom "UserId" claim
            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                userIdClaim = User.FindFirst("UserId")?.Value;
            }

            // Try parse integer
            return int.TryParse(userIdClaim, out userId);
        }


        // ============================================================
        // GET: api/User/groups
        // Get all groups
        // ============================================================
        [HttpGet("groups")]
        public async Task<IActionResult> GetGroups()
        {
            try
            {
                var groups = await _context.GroupsUsers
                    .Include(g => g.GroupMembers)
                    .ThenInclude(m => m.User)
                    .Select(g => new
                    {
                        id = g.Id,

                        name = g.Name,

                        createdBy = g.CreatedBy,

                        createdAt = g.CreatedAt,

                        members = g.GroupMembers.Select(m => new
                        {
                            id = m.Id,

                            displayName = m.User != null
                                ? (
                                    (m.User.FirstName ?? "") +
                                    " " +
                                    (m.User.LastName ?? "")
                                  ).Trim()
                                : m.Name,

                            name = m.User != null
                                ? (
                                    (m.User.FirstName ?? "") +
                                    " " +
                                    (m.User.LastName ?? "")
                                  ).Trim()
                                : m.Name,

                            phone = m.User != null
                                ? m.User.PhoneNumber
                                : m.Phone,

                            role = m.Role,

                            userId = m.UserId,

                            isRegistered = m.UserId != null
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Groups loaded successfully.",
                    data = groups
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load groups.",
                    error = ex.Message
                });
            }
        }


        // ============================================================
        // GET: api/User/notifications
        // Get notifications for logged-in user
        // ============================================================
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                // Get logged-in user
                if (!TryGetUserId(out int userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                var today = DateOnly.FromDateTime(DateTime.Today);
                var currentTime = TimeOnly.FromDateTime(DateTime.Now);

                var notifications = await _context.Notifications
                    .Include(n => n.Task)
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.SentAt)
                    .Select(n => new
                    {
                        id = n.Id,

                        taskTitle = n.Task != null
                            ? n.Task.Title
                            : null,

                        message = n.Message,

                        sentAt = n.SentAt,

                        isRead = n.IsRead,

                        // Check whether task is expired
                        isExpired =
                            n.Task != null &&
                            n.Task.DueDate.HasValue &&
                            (
                                n.Task.DueDate.Value < today
                                ||
                                (
                                    n.Task.DueDate.Value == today &&
                                    n.Task.DueTime.HasValue &&
                                    n.Task.DueTime.Value < currentTime
                                )
                            ),

                        // Check whether another active task
                        // exists at the same date/time
                        isClash =
                            n.Task != null &&
                            _context.Tasks.Any(t =>
                                t.Id != n.Task.Id &&
                                t.AssignedTo == userId &&
                                t.DueDate == n.Task.DueDate &&
                                t.DueTime == n.Task.DueTime &&
                                t.Status != "Done")
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Notifications loaded successfully.",
                    data = notifications
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load notifications.",
                    error = ex.Message
                });
            }
        }


        // ============================================================
        // GET: api/User/settings
        // Get user settings
        // ============================================================
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            try
            {
                // Get logged-in user
                if (!TryGetUserId(out int userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                var setting = await _context.UserSettings
                    .FirstOrDefaultAsync(x => x.UserId == userId);

                // Return default settings if no record exists
                if (setting == null)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Default settings loaded.",
                        data = new
                        {
                            notificationEnabled = true,
                            darkModeEnabled = false,
                            snoozeMinutes = 5
                        }
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Settings loaded successfully.",
                    data = new
                    {
                        notificationEnabled =
                            setting.NotificationEnabled,

                        darkModeEnabled =
                            setting.DarkModeEnabled,

                        snoozeMinutes =
                            setting.SnoozeMinutes
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load settings.",
                    error = ex.Message
                });
            }
        }


        // ============================================================
        // PUT: api/User/settings
        // Save / Update user settings
        // ============================================================
        [HttpPut("settings")]
        public async Task<IActionResult> SaveSettings([FromBody] UpdateSettingsDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Request body is required."
                    });
                }

                // Get logged-in user
                if (!TryGetUserId(out int userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                // Validate snooze value
                if (dto.SnoozeMinutes < 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Snooze minutes cannot be negative."
                    });
                }

                var setting = await _context.UserSettings
                    .FirstOrDefaultAsync(x => x.UserId == userId);

                // Create settings if they don't exist
                if (setting == null)
                {
                    setting = new UserSetting
                    {
                        UserId = userId,

                        NotificationEnabled =
                            dto.NotificationEnabled,

                        DarkModeEnabled =
                            dto.DarkModeEnabled,

                        SnoozeMinutes =
                            dto.SnoozeMinutes,

                        UpdatedAt = DateTime.Now
                    };

                    _context.UserSettings.Add(setting);
                }
                else
                {
                    // Update existing settings
                    setting.NotificationEnabled =
                        dto.NotificationEnabled;

                    setting.DarkModeEnabled =
                        dto.DarkModeEnabled;

                    setting.SnoozeMinutes =
                        dto.SnoozeMinutes;

                    setting.UpdatedAt =
                        DateTime.Now;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Settings updated successfully.",
                    data = new
                    {
                        notificationEnabled =
                            setting.NotificationEnabled,

                        darkModeEnabled =
                            setting.DarkModeEnabled,

                        snoozeMinutes =
                            setting.SnoozeMinutes
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to update settings.",
                    error = ex.Message
                });
            }
        }


        // ============================================================
        // GET: api/User/tasks
        // Get tasks belonging to logged-in user
        // ============================================================
        [HttpGet("tasks")]
        public async Task<IActionResult> GetTasks([FromQuery] bool isTimeBased = true)
        {
            try
            {
                // Get logged-in user
                if (!TryGetUserId(out int userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                var tasks = await _context.Tasks
                    .Where(t =>
                        t.IsTimeBased == isTimeBased &&
                        (
                            t.CreatedBy == userId ||
                            t.AssignedTo == userId
                        ))
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new
                    {
                        id = t.Id,

                        title = t.Title,

                        description = t.Description,

                        dueDate = t.DueDate,

                        dueTime = t.DueTime,

                        isCompleted =
                            t.Status == "Done",

                        status = t.Status,

                        isTimeBased =
                            t.IsTimeBased,

                        groupId =
                            t.GroupId,

                        createdBy =
                            t.CreatedBy,

                        assignedTo =
                            t.AssignedTo,

                        createdAt =
                            t.CreatedAt,

                        updatedAt =
                            t.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Tasks loaded successfully.",
                    data = tasks
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load tasks.",
                    error = ex.Message
                });
            }
        }
    }


    // ================================================================
    // DTO: Update User Settings
    // ================================================================
    public class UpdateSettingsDto
    {
        public bool NotificationEnabled { get; set; }

        public bool DarkModeEnabled { get; set; }

        public int SnoozeMinutes { get; set; }
    }
}












//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using System.Security.Claims;
//using TODOLISTAPI.Models;

//namespace TODOLISTAPI.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class UserController : ControllerBase
//    {
//        private readonly TodoSmartAlertsContext _context;

//        public UserController(TodoSmartAlertsContext context)
//        {
//            _context = context;
//        }

//        // GET: api/groups
//        [HttpGet]
//        public async Task<IActionResult> GetGroups()
//        {
//            try
//            {
//                var groups = await _context.GroupsUsers
//                    .Include(g => g.GroupMembers)
//                        .ThenInclude(m => m.User)
//                    .Select(g => new
//                    {
//                        id = g.Id,
//                        name = g.Name,

//                        members = g.GroupMembers.Select(m => new
//                        {
//                            id = m.Id,

//                            // Frontend expects displayName
//                            displayName = m.User != null
//                                ? m.User.FirstName + " " + m.User.LastName
//                                : m.Name,

//                            name = m.Name,

//                            phone = m.User != null
//                                ? m.User.PhoneNumber
//                                : m.Phone,

//                            role = m.Role,

//                            isRegistered = m.UserId != null
//                        }).ToList()
//                    })
//                    .ToListAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Groups loaded successfully.",
//                    data = groups
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    message = ex.Message
//                });
//            }
//        }

//        [HttpGet]
//        public async Task<IActionResult> GetNotifications()
//        {
//            try
//            {
//                // User Id stored inside JWT Token
//                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

//                if (string.IsNullOrEmpty(userIdClaim))
//                {
//                    return Unauthorized(new
//                    {
//                        success = false,
//                        message = "Invalid Token"
//                    });
//                }

//                int userId = Convert.ToInt32(userIdClaim);

//                var notifications = await _context.Notifications
//                    .Include(n => n.Task)
//                    .Where(n => n.UserId == userId)
//                    .OrderByDescending(n => n.SentAt)
//                    .Select(n => new
//                    {
//                        id = n.Id,

//                        taskTitle = n.Task.Title,

//                        message = n.Message,

//                        sentAt = n.SentAt,

//                        isRead = n.IsRead,

//                        // Task overdue
//                        isExpired =
//                            n.Task.DueDate.HasValue &&
//                            (
//                                n.Task.DueDate.Value < DateOnly.FromDateTime(DateTime.Today)
//                                ||
//                                (
//                                    n.Task.DueDate.Value == DateOnly.FromDateTime(DateTime.Today) &&
//                                    n.Task.DueTime.HasValue &&
//                                    n.Task.DueTime.Value < TimeOnly.FromDateTime(DateTime.Now)
//                                )
//                            ),

//                        // Another task exists at same date & time
//                        isClash = _context.Tasks.Any(t =>
//                                t.Id != n.Task.Id &&
//                                t.AssignedTo == userId &&
//                                t.DueDate == n.Task.DueDate &&
//                                t.DueTime == n.Task.DueTime &&
//                                t.Status != "Done")
//                    })
//                    .ToListAsync();

//                return Ok(new
//                {
//                    success = true,
//                    data = notifications
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    message = ex.Message
//                });
//            }
//        }

//        [HttpGet]
//        public async Task<IActionResult> GetSettings()
//        {
//            int userId = Convert.ToInt32(
//                User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

//            var setting = await _context.UserSettings
//                .FirstOrDefaultAsync(x => x.UserId == userId);

//            if (setting == null)
//            {
//                return Ok(new
//                {
//                    success = true,
//                    data = new
//                    {
//                        notificationEnabled = true,
//                        darkModeEnabled = false,
//                        snoozeMinutes = 5
//                    }
//                });
//            }

//            return Ok(new
//            {
//                success = true,
//                data = new
//                {
//                    notificationEnabled = setting.NotificationEnabled,
//                    darkModeEnabled = setting.DarkModeEnabled,
//                    snoozeMinutes = setting.SnoozeMinutes
//                }
//            });
//        }

//        //----------------------------------------------------
//        // SAVE SETTINGS
//        //----------------------------------------------------
//        [HttpPut]
//        public async Task<IActionResult> SaveSettings(UpdateSettingsDto dto)
//        {
//            int userId = Convert.ToInt32(
//                User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

//            var setting = await _context.UserSettings
//                .FirstOrDefaultAsync(x => x.UserId == userId);

//            if (setting == null)
//            {
//                setting = new UserSetting
//                {
//                    UserId = userId,
//                    NotificationEnabled = dto.NotificationEnabled,
//                    DarkModeEnabled = dto.DarkModeEnabled,
//                    SnoozeMinutes = dto.SnoozeMinutes,
//                    UpdatedAt = DateTime.Now
//                };

//                _context.UserSettings.Add(setting);
//            }
//            else
//            {
//                setting.NotificationEnabled = dto.NotificationEnabled;
//                setting.DarkModeEnabled = dto.DarkModeEnabled;
//                setting.SnoozeMinutes = dto.SnoozeMinutes;
//                setting.UpdatedAt = DateTime.Now;
//            }

//            await _context.SaveChangesAsync();

//            return Ok(new
//            {
//                success = true,
//                message = "Settings updated successfully."
//            });
//        }


//        [HttpGet]
//        public async Task<IActionResult> GetTasks([FromQuery] bool isTimeBased = true)
//        {
//            try
//            {
//                var claim = User.FindFirst(ClaimTypes.NameIdentifier);

//                if (claim == null)
//                {
//                    return Unauthorized(new
//                    {
//                        success = false,
//                        message = "Invalid token."
//                    });
//                }

//                int userId = Convert.ToInt32(claim.Value);

//                var tasks = await _context.Tasks
//                    .Where(t =>
//                        t.IsTimeBased == isTimeBased &&
//                        (
//                            t.CreatedBy == userId ||
//                            t.AssignedTo == userId
//                        ))
//                    .OrderByDescending(t => t.CreatedAt)
//                    .Select(t => new
//                    {
//                        id = t.Id,

//                        title = t.Title,

//                        description = t.Description,

//                        dueDate = t.DueDate,

//                        dueTime = t.DueTime,

//                        isCompleted = t.Status == "Done",

//                        status = t.Status,

//                        isTimeBased = t.IsTimeBased,

//                        createdAt = t.CreatedAt
//                    })
//                    .ToListAsync();

//                return Ok(new
//                {
//                    success = true,
//                    data = tasks
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    message = ex.Message
//                });
//            }
//        }

//    }

//    public class UpdateSettingsDto
//    {
//        public bool NotificationEnabled { get; set; }

//        public bool DarkModeEnabled { get; set; }

//        public int SnoozeMinutes { get; set; }
//    }
//}
