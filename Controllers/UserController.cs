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

        private bool TryGetUserId(out int userId)
        {
            userId = 0;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                userIdClaim = User.FindFirst("UserId")?.Value;
            }

            return int.TryParse(userIdClaim, out userId);
        }

        [HttpGet("groups")]
        public async Task<IActionResult> GetGroups()
        {
            try
            {
                if (!TryGetUserId(out int userId)) return Unauthorized();
                var groups = await _context.GroupsUsers
                    .Where(g => g.CreatedBy == userId ||
                        g.GroupMembers.Any(m => m.UserId == userId))
                    .Include(g => g.GroupMembers)
                        .ThenInclude(m => m.User)
                    .Select(g => new
                    {
                        id = g.Id,
                        groupId = g.Id,
                        name = g.Name,
                        createdBy = g.CreatedBy,
                        createdAt = g.CreatedAt,

                        members = g.GroupMembers
                            .Select(m => new
                            {
                                id = m.Id,

                                userId = m.UserId,

                                displayName = m.User != null
                                    ? (
                                        ((m.User.FirstName ?? "") + " " +
                                         (m.User.LastName ?? "")).Trim()
                                      )
                                    : (m.Name ?? ""),

                                name = m.User != null
                                    ? (
                                        ((m.User.FirstName ?? "") + " " +
                                         (m.User.LastName ?? "")).Trim()
                                      )
                                    : (m.Name ?? ""),

                                phone = m.User != null
                                    ? m.User.PhoneNumber
                                    : m.Phone,

                                role = m.Role,

                                isRegistered = m.UserId != null
                            })
                            .ToList()
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
                HttpContext.RequestServices.GetRequiredService<ILogger<UserController>>().LogError(ex, "Request failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load groups.",
                    error = "An internal server error occurred."
                });
            }
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
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
                HttpContext.RequestServices.GetRequiredService<ILogger<UserController>>().LogError(ex, "Request failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load notifications.",
                    error = "An internal server error occurred."
                });
            }
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            try
            {
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
                HttpContext.RequestServices.GetRequiredService<ILogger<UserController>>().LogError(ex, "Request failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load settings.",
                    error = "An internal server error occurred."
                });
            }
        }

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

                if (!TryGetUserId(out int userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                if (dto.SnoozeMinutes < 1 || dto.SnoozeMinutes > 1440)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Snooze minutes must be between 1 and 1440."
                    });
                }

                var setting = await _context.UserSettings
                    .FirstOrDefaultAsync(x => x.UserId == userId);

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
                HttpContext.RequestServices.GetRequiredService<ILogger<UserController>>().LogError(ex, "Request failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to update settings.",
                    error = "An internal server error occurred."
                });
            }
        }

        [HttpPost("location")]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationDto dto)
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

                if (!TryGetUserId(out int userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                if (!double.IsFinite(dto.Latitude) || dto.Latitude < -90 || dto.Latitude > 90)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Latitude must be between -90 and 90."
                    });
                }

                if (!double.IsFinite(dto.Longitude) || dto.Longitude < -180 || dto.Longitude > 180)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Longitude must be between -180 and 180."
                    });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "User not found."
                    });
                }

                if (dto.Accuracy.HasValue && (!double.IsFinite(dto.Accuracy.Value) || dto.Accuracy.Value < 0))
                    return BadRequest(new { success = false, message = "Accuracy must be finite and non-negative." });
                var reportedAt = DateTime.Now;

                user.LastLatitude = dto.Latitude;
                user.LastLongitude = dto.Longitude;
                user.LastLocationUpdatedAt = reportedAt;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,

                    message = "Location updated successfully.",

                    data = new
                    {
                        latitude = dto.Latitude,

                        longitude = dto.Longitude,

                        accuracy = dto.Accuracy,

                        updatedAt = reportedAt
                    }
                });
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<UserController>>().LogError(ex, "Request failed");
                Console.WriteLine("UpdateLocation Error: " + ex.Message);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to update location."
                });
            }
        }

        [HttpGet("location")]
        public async Task<IActionResult> GetLocation()
        {
            try
            {
                if (!TryGetUserId(out int userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new
                    {
                        u.LastLatitude,
                        u.LastLongitude,
                        u.LastLocationUpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "User not found."
                    });
                }

                bool neverReported =
                    user.LastLatitude == null ||
                    user.LastLongitude == null;

                return Ok(new
                {
                    success = true,

                    message = neverReported
                        ? "No location has been reported yet."
                        : "Last known location loaded successfully.",

                    data = neverReported
                        ? null
                        : new
                        {
                            latitude = user.LastLatitude,

                            longitude = user.LastLongitude,

                            updatedAt = user.LastLocationUpdatedAt
                        }
                });
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<UserController>>().LogError(ex, "Request failed");
                Console.WriteLine("GetLocation Error: " + ex.Message);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load location."
                });
            }
        }

        [HttpGet("tasks")]
        public async Task<IActionResult> GetTasks([FromQuery] bool isTimeBased = true)
        {
            try
            {
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

                        geofenceEnabled = t.GeofenceEnabled,
                        latitude = t.Latitude,
                        longitude = t.Longitude,

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
                HttpContext.RequestServices.GetRequiredService<ILogger<UserController>>().LogError(ex, "Request failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load tasks.",
                    error = "An internal server error occurred."
                });
            }
        }
    }

    public class PersonalContactRequest
    {
        public string? Name { get; set; }

        public string? Phone { get; set; }
    }

    public class UpdateSettingsDto
    {
        public bool NotificationEnabled { get; set; }

        public bool DarkModeEnabled { get; set; }

        public int SnoozeMinutes { get; set; }
    }

    public class UpdateLocationDto
    {
        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double? Accuracy { get; set; }
    }
}

