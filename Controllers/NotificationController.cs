using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TODOLISTAPI.Models;

namespace TODOLISTAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly TodoSmartAlertsContext _context;

        public NotificationController(TodoSmartAlertsContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                    return Unauthorized();

                if (!int.TryParse(userIdClaim, out int userId))
                    return Unauthorized();

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.SentAt)
                    .Select(n => new
                    {
                        id = n.Id,
                        taskId = n.TaskId,
                        senderId = n.SenderId,
                        type = n.Type,
                        message = n.Message,
                        isRead = n.IsRead,
                        sentAt = n.SentAt,

                        senderName = n.Sender != null
                            ? n.Sender.FirstName + " " + n.Sender.LastName
                            : null,

                        taskTitle = n.Task != null
                            ? n.Task.Title
                            : null
                    })
                    .ToListAsync();

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error while getting notifications.",
                    error = ex.Message
                });
            }
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == id &&
                    n.UserId == userId);

            if (notification == null)
                return NotFound();

            notification.IsRead = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true
            });
        }
    }
}














//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.EntityFrameworkCore;
//using TODOLISTAPI.Models;

//namespace TODOLISTAPI.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class NotificationController : ControllerBase
//    {
//        private readonly TodoSmartAlertsContext db;

//        public NotificationController(TodoSmartAlertsContext db)
//        {
//            this.db = db;
//        }

//        // =========================================================
//        // GET CURRENT LOGGED-IN USER ID FROM JWT
//        // =========================================================
//        private int? GetCurrentUserId()
//        {
//            string? userIdValue =
//                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
//                ?? User.FindFirst("id")?.Value
//                ?? User.FindFirst("userId")?.Value;

//            if (string.IsNullOrWhiteSpace(userIdValue))
//                return null;

//            if (!int.TryParse(userIdValue, out int userId))
//                return null;

//            return userId;
//        }

//        // =========================================================
//        // GET: api/Notification
//        //
//        // GET ALL NOTIFICATIONS OF CURRENT USER
//        // =========================================================
//        [HttpGet]
//        public async Task<IActionResult> GetAllNotifications()
//        {
//            try
//            {
//                int? userId = GetCurrentUserId();

//                if (userId == null)
//                {
//                    return Unauthorized(new
//                    {
//                        success = false,
//                        message = "Invalid or missing user token."
//                    });
//                }

//                var notifications = await db.Notifications
//                    .Where(n => n.UserId == userId.Value)
//                    .OrderByDescending(n => n.SentAt)
//                    .Select(n => new
//                    {
//                        id = n.Id,
//                        taskId = n.TaskId,
//                        userId = n.UserId,
//                        message = n.Message,
//                        isRead = n.IsRead,
//                        sentAt = n.SentAt,

//                        task = db.Tasks
//                            .Where(t => t.Id == n.TaskId)
//                            .Select(t => new
//                            {
//                                id = t.Id,
//                                title = t.Title,
//                                description = t.Description,
//                                isTimeBased = t.IsTimeBased,
//                                dueDate = t.DueDate,
//                                dueTime = t.DueTime,
//                                status = t.Status,
//                                groupId = t.GroupId
//                            })
//                            .FirstOrDefault()
//                    })
//                    .ToListAsync();

//                return Ok(new
//                {
//                    success = true,
//                    count = notifications.Count,
//                    data = notifications
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    message = "Failed to fetch notifications.",
//                    error = ex.Message
//                });
//            }
//        }

//        // =========================================================
//        // GET: api/Notification/due
//        //
//        // CHECK WHICH NOTIFICATIONS SHOULD POP UP NOW
//        // =========================================================
//        [HttpGet("due")]
//        public async Task<IActionResult> GetDueNotifications()
//        {
//            try
//            {
//                int? userId = GetCurrentUserId();

//                if (userId == null)
//                {
//                    return Unauthorized(new
//                    {
//                        success = false,
//                        message = "Invalid or missing user token."
//                    });
//                }

//                int currentUserId = userId.Value;

//                DateTime now = DateTime.Now;

//                // -------------------------------------------------
//                // GET TASKS BELONGING TO CURRENT USER
//                // -------------------------------------------------
//                var tasks = await db.Tasks
//                    .Where(t =>
//                        t.Status != "Done" &&
//                        t.Status != "Cancelled" &&
//                        (
//                            t.CreatedBy == currentUserId ||
//                            t.AssignedTo == currentUserId ||

//                            (
//                                t.GroupId != null &&
//                                    db.GroupMembers.Any(g =>
//                                    g.GroupId == t.GroupId &&
//                                    g.UserId == currentUserId
//                                )
//                            )
//                        )
//                    )
//                    .ToListAsync();

//                var result = new List<object>();

//                // =================================================
//                // CHECK EVERY TASK
//                // =================================================
//                foreach (var task in tasks)
//                {
//                    // =================================================
//                    // TIME-BASED TASK
//                    // =================================================
//                    if (task.IsTimeBased)
//                    {
//                        if (task.DueDate == null || task.DueTime == null)
//                            continue;

//                        /*
//                         * SQL DATE -> DateOnly
//                         * SQL TIME -> TimeOnly
//                         *
//                         * With modern EF Core scaffolding these normally
//                         * become DateOnly? and TimeOnly?.
//                         */
//                        DateTime dueDateTime =
//                            task.DueDate.Value.ToDateTime(
//                                task.DueTime.Value
//                            );

//                        /*
//                         * Give the app a small grace period because
//                         * React Native may poll every 20-30 seconds.
//                         *
//                         * Notification is considered due:
//                         *
//                         * due time <= current time
//                         *
//                         * and
//                         *
//                         * not older than 2 minutes.
//                         */
//                        DateTime earliestAllowed =
//                            now.AddMinutes(-2);

//                        bool isDue =
//                            dueDateTime <= now &&
//                            dueDateTime >= earliestAllowed;

//                        if (!isDue)
//                            continue;

//                        // ---------------------------------------------
//                        // CHECK IF ALREADY SENT
//                        // ---------------------------------------------
//                        DateTime minuteStart = new DateTime(
//                            dueDateTime.Year,
//                            dueDateTime.Month,
//                            dueDateTime.Day,
//                            dueDateTime.Hour,
//                            dueDateTime.Minute,
//                            0
//                        );

//                        DateTime minuteEnd =
//                            minuteStart.AddMinutes(1);

//                        bool alreadySent =
//                            await db.Notifications.AnyAsync(n =>
//                                n.TaskId == task.Id &&
//                                n.UserId == currentUserId &&
//                                n.SentAt >= minuteStart &&
//                                n.SentAt < minuteEnd
//                            );

//                        if (alreadySent)
//                            continue;

//                        string message =
//                            $"Reminder: {task.Title}";

//                        // ---------------------------------------------
//                        // CREATE NOTIFICATION
//                        // ---------------------------------------------
//                        var notification = new Notification
//                        {
//                            TaskId = task.Id,
//                            UserId = currentUserId,
//                            Message = message,
//                            IsRead = false,
//                            SentAt = now
//                        };

//                        db.Notifications.Add(notification);

//                        await db.SaveChangesAsync();

//                        result.Add(new
//                        {
//                            notificationId = notification.Id,
//                            taskId = task.Id,

//                            title = task.Title,
//                            description = task.Description,

//                            isTimeBased = true,

//                            dueDate = task.DueDate,
//                            dueTime = task.DueTime,

//                            message = message,

//                            notificationType = "TimeBased"
//                        });
//                    }

//                    // =================================================
//                    // NON-TIME-BASED TASK
//                    // =================================================
//                    else
//                    {
//                        /*
//                         * Non-time based reminder times:
//                         *
//                         * Morning = 09:00 AM
//                         * Evening = 06:00 PM
//                         */

//                        const int MORNING_HOUR = 9;
//                        const int EVENING_HOUR = 18;

//                        string? reminderSlot = null;

//                        // ---------------------------------------------
//                        // MORNING SLOT
//                        // 09:00 AM -> before 06:00 PM
//                        // ---------------------------------------------
//                        if (
//                            now.Hour >= MORNING_HOUR &&
//                            now.Hour < EVENING_HOUR
//                        )
//                        {
//                            reminderSlot = "Morning";
//                        }

//                        // ---------------------------------------------
//                        // EVENING SLOT
//                        // after 06:00 PM
//                        // ---------------------------------------------
//                        else if (now.Hour >= EVENING_HOUR)
//                        {
//                            reminderSlot = "Evening";
//                        }

//                        // Before 9 AM = no notification
//                        if (reminderSlot == null)
//                            continue;

//                        DateTime todayStart = now.Date;
//                        DateTime tomorrowStart =
//                            todayStart.AddDays(1);

//                        /*
//                         * We use Message to identify whether
//                         * today's morning/evening reminder was sent.
//                         *
//                         * Therefore no database modification
//                         * is required.
//                         */

//                        string notificationMarker =
//                            reminderSlot == "Morning"
//                                ? "[NON_TIME_MORNING]"
//                                : "[NON_TIME_EVENING]";

//                        bool alreadySent =
//                            await db.Notifications.AnyAsync(n =>
//                                n.TaskId == task.Id &&
//                                n.UserId == currentUserId &&
//                                n.SentAt >= todayStart &&
//                                n.SentAt < tomorrowStart &&
//                                n.Message != null &&
//                                n.Message.StartsWith(notificationMarker)
//                            );

//                        if (alreadySent)
//                            continue;

//                        string message =
//                            $"{notificationMarker} Reminder: {task.Title}";

//                        // ---------------------------------------------
//                        // SAVE NOTIFICATION
//                        // ---------------------------------------------
//                        var notification = new Notification
//                        {
//                            TaskId = task.Id,
//                            UserId = currentUserId,
//                            Message = message,
//                            IsRead = false,
//                            SentAt = now
//                        };

//                        db.Notifications.Add(notification);

//                        await db.SaveChangesAsync();

//                        result.Add(new
//                        {
//                            notificationId = notification.Id,

//                            taskId = task.Id,

//                            title = task.Title,

//                            description = task.Description,

//                            isTimeBased = false,

//                            dueDate = task.DueDate,

//                            dueTime = task.DueTime,

//                            message = message
//                                .Replace(notificationMarker, "")
//                                .Trim(),

//                            notificationType =
//                                reminderSlot == "Morning"
//                                    ? "NonTimeBasedMorning"
//                                    : "NonTimeBasedEvening"
//                        });
//                    }
//                }

//                return Ok(new
//                {
//                    success = true,

//                    hasNotification = result.Count > 0,

//                    count = result.Count,

//                    data = result
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    message =
//                        "Failed to check due notifications.",

//                    error = ex.Message
//                });
//            }
//        }

//        // =========================================================
//        // PUT: api/Notification/5/read
//        //
//        // MARK ONE NOTIFICATION AS READ
//        // =========================================================
//        [HttpPut("{id}/read")]
//        public async Task<IActionResult> MarkAsRead(int id)
//        {
//            try
//            {
//                int? userId = GetCurrentUserId();

//                if (userId == null)
//                {
//                    return Unauthorized(new
//                    {
//                        success = false,
//                        message = "Invalid user."
//                    });
//                }

//                var notification =
//                    await db.Notifications
//                        .FirstOrDefaultAsync(n =>
//                            n.Id == id &&
//                            n.UserId == userId.Value
//                        );

//                if (notification == null)
//                {
//                    return NotFound(new
//                    {
//                        success = false,
//                        message = "Notification not found."
//                    });
//                }

//                notification.IsRead = true;

//                await db.SaveChangesAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Notification marked as read."
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    message =
//                        "Failed to update notification.",

//                    error = ex.Message
//                });
//            }
//        }

//        // =========================================================
//        // PUT: api/Notification/read-all
//        // =========================================================
//        [HttpPut("read-all")]
//        public async Task<IActionResult> MarkAllAsRead()
//        {
//            try
//            {
//                int? userId = GetCurrentUserId();

//                if (userId == null)
//                {
//                    return Unauthorized(new
//                    {
//                        success = false,
//                        message = "Invalid user."
//                    });
//                }

//                var notifications =
//                    await db.Notifications
//                        .Where(n =>
//                            n.UserId == userId.Value &&
//                            !n.IsRead
//                        )
//                        .ToListAsync();

//                foreach (var notification in notifications)
//                {
//                    notification.IsRead = true;
//                }

//                await db.SaveChangesAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "All notifications marked as read."
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    message =
//                        "Failed to update notifications.",

//                    error = ex.Message
//                });
//            }
//        }
//    }
//}
