using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TODOLISTAPI.Models;

namespace TODOLISTAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskController : ControllerBase
    {
        private readonly TodoSmartAlertsContext _context;

        public TaskController(TodoSmartAlertsContext context)
        {
            _context = context;
        }

        // ============================================================
        // GET CURRENT USER ID
        // ============================================================
        private int? GetCurrentUserId()
        {
            var claim =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("UserId");

            if (int.TryParse(claim, out int userId))
            {
                return userId;
            }

            return null;
        }

        // ============================================================
        // POST: api/Task/{id}/done
        // Mark task as completed
        // ============================================================
        [HttpPost("{id}/done")]
        public async Task<IActionResult> MarkDone(int id)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (userId == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User is not authenticated."
                    });
                }

                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t =>
                        t.Id == id &&
                        (
                            t.AssignedTo == userId.Value ||
                            (
                                t.AssignedTo == null &&
                                t.CreatedBy == userId.Value
                            )
                        ));

                if (task == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Task not found."
                    });
                }

                if (task.Status == "Done")
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Task is already marked as done.",
                        taskId = task.Id,
                        status = task.Status
                    });
                }

                task.Status = "Done";
                task.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Task marked as done successfully.",
                    taskId = task.Id,
                    status = task.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ============================================================
        // GET: api/Task/groups
        // Get groups with members
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

                            isRegistered = m.UserId != null,
                            memberCount = g.GroupMembers.Count()
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
        // GET: api/Task/group
        //
        // Example:
        // /api/Task/group?groupId=1&isTimeBased=true&tab=today
        //
        // Group dashboard tasks
        // ============================================================
        [HttpGet("group")]
        public async Task<IActionResult> GetGroupTasks(
            [FromQuery] string? tab,
            [FromQuery] bool isTimeBased,
            [FromQuery] int groupId)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Today);

                IQueryable<Models.Task> query = _context.Tasks
                    .AsNoTracking()
                    .Where(x =>
                        x.GroupId == groupId &&
                        x.IsTimeBased == isTimeBased &&
                        x.Status != "Done" &&
                        x.Status != "Cancelled");

                switch ((tab ?? "").ToLower())
                {
                    case "today":

                        query = query.Where(x =>
                            x.DueDate == today);

                        break;

                    case "pending":

                        query = query.Where(x =>
                            x.Status == "Pending" ||
                            x.Status == "Snoozed");

                        break;

                    case "upcoming":

                        query = query.Where(x =>
                            x.DueDate > today);

                        break;
                }

                var tasks = await query
                    .OrderBy(x => x.DueDate)
                    .ThenBy(x => x.DueTime)
                    .Select(x => new
                    {
                        id = x.Id,
                        title = x.Title,
                        description = x.Description,
                        dueDate = x.DueDate,
                        dueTime = x.DueTime,
                        isTimeBased = x.IsTimeBased,
                        isCompleted = x.Status == "Done",
                        status = x.Status,
                        groupId = x.GroupId,
                        createdBy = x.CreatedBy,
                        assignedTo = x.AssignedTo,
                        createdAt = x.CreatedAt,
                        updatedAt = x.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Group tasks fetched successfully.",
                    data = tasks
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ============================================================
        // GET: api/Task/personal
        //
        // HOME DASHBOARD ENDPOINT
        //
        // IMPORTANT:
        // Only returns tasks belonging to the logged-in user.
        // Completed and cancelled tasks are NOT returned.
        //
        // Example:
        // /api/Task/personal?isTimeBased=true&tab=today
        // ============================================================
        [HttpGet("personal")]
        public async Task<IActionResult> GetPersonalTasks(
            [FromQuery] string? tab,
            [FromQuery] bool isTimeBased)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (userId == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User is not authenticated."
                    });
                }

                var today = DateOnly.FromDateTime(DateTime.Today);

                // ====================================================
                // ONLY TASKS BELONGING TO CURRENT USER
                // ====================================================
                IQueryable<Models.Task> query = _context.Tasks
                    .AsNoTracking()
                    .Where(t =>
                        t.GroupId == null &&
                        t.IsTimeBased == isTimeBased &&
                        (
                            t.AssignedTo == userId.Value ||
                            (
                                t.AssignedTo == null &&
                                t.CreatedBy == userId.Value
                            )
                        ) &&
                        t.Status != "Done" &&
                        t.Status != "Cancelled");

                // ====================================================
                // TAB FILTER
                // ====================================================
                switch ((tab ?? "").ToLower())
                {
                    case "today":

                        query = query.Where(t =>
                            t.DueDate == today);

                        break;

                    case "pending":

                        query = query.Where(t =>
                            t.Status == "Pending" ||
                            t.Status == "Snoozed");

                        break;

                    case "upcoming":

                        query = query.Where(t =>
                            t.DueDate > today);

                        break;

                    case "":

                        // ALL
                        // No additional date filter.
                        break;
                }

                // ====================================================
                // HOME DASHBOARD RESPONSE
                //
                // Only send fields actually required by dashboard.
                // ====================================================
                var tasks = await query
                    .OrderBy(t => t.DueDate)
                    .ThenBy(t => t.DueTime)
                    .Select(t => new
                    {
                        id = t.Id,

                        title = t.Title,

                        description = t.Description,

                        dueDate = t.DueDate,

                        dueTime = t.DueTime,

                        isTimeBased = t.IsTimeBased,

                        status = t.Status,

                        isCompleted = false,

                        groupId = t.GroupId,

                        // The saved place, so the app can show the reminder
                        // and open the right editor (a Location Based task
                        // has no date and no time).
                        latitude = t.Latitude,

                        longitude = t.Longitude,

                        geofenceRadiusMeters = t.GeofenceRadiusMeters,

                        geofenceEnabled = t.GeofenceEnabled
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Personal dashboard tasks fetched successfully.",
                    count = tasks.Count,
                    data = tasks
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to fetch personal dashboard tasks.",
                    error = ex.Message
                });
            }
        }

        // ============================================================
        // GET: api/Task
        //
        // General task endpoint.
        // This endpoint is NOT used by HomeDashboard.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetTasks(
            [FromQuery] bool isTimeBased)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (userId == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User is not authenticated."
                    });
                }

                var tasks = await _context.Tasks
                    .AsNoTracking()
                    .Where(t =>
                        t.IsTimeBased == isTimeBased &&
                        (
                            t.AssignedTo == userId.Value ||
                            (
                                t.AssignedTo == null &&
                                t.CreatedBy == userId.Value
                            )
                        ))
                    .OrderBy(t => t.Status == "Done")
                    .ThenBy(t => t.DueDate)
                    .ThenBy(t => t.DueTime)
                    .Select(t => new
                    {
                        id = t.Id,
                        title = t.Title,
                        description = t.Description,
                        dueDate = t.DueDate,
                        dueTime = t.DueTime,
                        isTimeBased = t.IsTimeBased,
                        status = t.Status,
                        isCompleted = t.Status == "Done",
                        groupId = t.GroupId,
                        createdBy = t.CreatedBy,
                        assignedTo = t.AssignedTo,
                        createdAt = t.CreatedAt,
                        updatedAt = t.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Tasks fetched successfully.",
                    data = tasks
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ============================================================
        // DELETE: api/Task/task/{id}
        // Delete a task
        // ============================================================
        [HttpDelete("task/{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (userId == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User is not authenticated."
                    });
                }

                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t =>
                        t.Id == id &&
                        (
                            t.AssignedTo == userId.Value ||
                            (
                                t.AssignedTo == null &&
                                t.CreatedBy == userId.Value
                            )
                        ));

                if (task == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Task not found."
                    });
                }

                _context.Tasks.Remove(task);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Task deleted successfully.",
                    taskId = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ============================================================
        // DELETE: api/Task/group/{id}
        // Delete group, tasks and members
        // ============================================================
        [HttpDelete("group/{id}")]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            try
            {
                var group = await _context.GroupsUsers
                    .Include(g => g.Tasks)
                    .Include(g => g.GroupMembers)
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (group == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Group not found."
                    });
                }

                if (group.Tasks != null && group.Tasks.Any())
                {
                    _context.Tasks.RemoveRange(group.Tasks);
                }

                if (group.GroupMembers != null && group.GroupMembers.Any())
                {
                    _context.GroupMembers.RemoveRange(group.GroupMembers);
                }

                _context.GroupsUsers.Remove(group);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Group deleted successfully.",
                    groupId = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ============================================================
        // POST: api/Task/{id}/snooze
        // Snooze a task
        // ============================================================
        [HttpPost("{id}/snooze")]
        public async Task<IActionResult> SnoozeTask(
            int id,
            [FromBody] SnoozeTaskRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (userId == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User is not authenticated."
                    });
                }

                if (request == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Request body is required."
                    });
                }

                if (request.MinutesToSnooze <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "MinutesToSnooze must be greater than zero."
                    });
                }

                var task = await _context.Tasks
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        (
                            x.AssignedTo == userId.Value ||
                            (
                                x.AssignedTo == null &&
                                x.CreatedBy == userId.Value
                            )
                        ));

                if (task == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Task not found."
                    });
                }

                if (task.Status == "Done")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Completed task cannot be snoozed."
                    });
                }

                if (task.DueDate == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Task has no due date."
                    });
                }

                if (task.DueTime == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Task has no due time."
                    });
                }

                DateTime currentDueDateTime =
                    task.DueDate.Value.ToDateTime(task.DueTime.Value);

                DateTime newWakeTime =
                    currentDueDateTime.AddMinutes(
                        request.MinutesToSnooze);

                task.DueDate =
                    DateOnly.FromDateTime(newWakeTime);

                task.DueTime =
                    TimeOnly.FromDateTime(newWakeTime);

                task.Status = "Snoozed";
                task.UpdatedAt = DateTime.Now;

                var snoozeLog = new SnoozeLog
                {
                    TaskId = task.Id,
                    UserId = userId.Value,
                    SnoozedAt = DateTime.Now,
                    WakeAt = newWakeTime
                };

                _context.SnoozeLogs.Add(snoozeLog);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message =
                        $"Task snoozed for {request.MinutesToSnooze} minutes.",
                    taskId = task.Id,
                    wakeAt = newWakeTime,
                    dueDate = task.DueDate,
                    dueTime = task.DueTime,
                    status = task.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ============================================================
        // GET: api/Task/clashes
        // ============================================================
        [HttpGet("clashes")]
        public async Task<IActionResult> GetClashes()
        {
            try
            {
                var userId = GetCurrentUserId();

                if (userId == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User is not authenticated."
                    });
                }

                var tasks = await _context.Tasks
                    .AsNoTracking()
                    .Include(t => t.Group)
                    .Where(t =>
                        (
                            t.AssignedTo == userId.Value ||
                            (
                                t.AssignedTo == null &&
                                t.CreatedBy == userId.Value
                            )
                        ) &&
                        t.Status != "Done" &&
                        t.Status != "Cancelled" &&
                        t.DueDate != null &&
                        t.DueTime != null &&
                        t.IsTimeBased)
                    .OrderBy(t => t.DueDate)
                    .ThenBy(t => t.DueTime)
                    .ToListAsync();

                var clashGroups = tasks
                    .GroupBy(t => new
                    {
                        t.DueDate,
                        t.DueTime
                    })
                    .Where(g => g.Count() > 1)
                    .ToList();

                var result = clashGroups.Select(g => new
                {
                    dueDate = g.Key.DueDate,
                    dueTime = g.Key.DueTime,

                    tasks = g.Select(t => new
                    {
                        id = t.Id,
                        title = t.Title,
                        dueDate = t.DueDate,
                        dueTime = t.DueTime,
                        groupName =
                            t.Group != null
                                ? t.Group.Name
                                : null
                    })
                });

                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // ============================================================
        // GET: api/Task/geofences
        //
        // Place-reminder feed for the mobile app.
        //
        // Returns the logged-in user's tasks that carry a saved place, so
        // the device can raise a "you are near this task" alert when it
        // comes within GeofenceRadiusMeters of the point.
        //
        // Only tasks that can still be acted on are included:
        //   * not Done, not Cancelled
        //   * GeofenceEnabled = true
        //   * Latitude and Longitude both present
        //
        // This is also how a Location Based task is identified: it is the
        // one that has a place saved.
        // ============================================================
        [HttpGet("geofences")]
        public async Task<IActionResult> GetGeofences()
        {
            try
            {
                var userId = GetCurrentUserId();

                if (userId == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User is not authenticated."
                    });
                }

                var geofences = await _context.Tasks
                    .AsNoTracking()
                    .Include(t => t.Group)
                    .Where(t =>
                        (
                            t.AssignedTo == userId.Value ||
                            (
                                t.AssignedTo == null &&
                                t.CreatedBy == userId.Value
                            )
                        ) &&
                        t.Status != "Done" &&
                        t.Status != "Cancelled" &&
                        t.GeofenceEnabled &&
                        t.Latitude != null &&
                        t.Longitude != null)
                    .OrderBy(t => t.DueDate)
                    .ThenBy(t => t.DueTime)
                    .Select(t => new
                    {
                        id = t.Id,

                        title = t.Title,

                        description = t.Description,

                        latitude = t.Latitude,

                        longitude = t.Longitude,

                        geofenceRadiusMeters = t.GeofenceRadiusMeters,

                        status = t.Status,

                        dueDate = t.DueDate,

                        dueTime = t.DueTime,

                        isTimeBased = t.IsTimeBased,

                        groupName =
                            t.Group != null
                                ? t.Group.Name
                                : null
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,

                    message = "Geofence targets loaded successfully.",

                    count = geofences.Count,

                    data = geofences
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetGeofences Error: " + ex.Message);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load geofence targets."
                });
            }
        }


        // ============================================================
        // GET: api/Task/history
        //
        // History Screen API
        //
        // Returns three separate task lists:
        //
        // 1. Pending
        // 2. Done
        // 3. Upcoming / Today
        //
        // Only returns tasks belonging to the logged-in user.
        // Supports both time-based and non-time-based tasks.
        //
        // Example:
        // GET /api/Task/history?isTimeBased=true
        // GET /api/Task/history?isTimeBased=false
        // ============================================================
        [HttpGet("history")]
        public async Task<IActionResult> GetTaskHistory([FromQuery] bool isTimeBased)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (userId == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User is not authenticated."
                    });
                }

                var today = DateOnly.FromDateTime(DateTime.Today);

                // ========================================================
                // BASE QUERY
                //
                // Only tasks belonging to logged-in user.
                // Only selected task type.
                // ========================================================
                var baseQuery = _context.Tasks
                    .AsNoTracking()
                    .Where(t =>
                        t.IsTimeBased == isTimeBased &&
                        (
                            t.AssignedTo == userId.Value ||
                            (
                                t.AssignedTo == null &&
                                t.CreatedBy == userId.Value
                            )
                        ));

                // ========================================================
                // PENDING TASKS
                //
                // Pending + Snoozed
                // ========================================================
                var pendingTasks = await baseQuery
                    .Where(t =>
                        t.Status == "Pending" ||
                        t.Status == "Snoozed")
                    .OrderBy(t => t.DueDate)
                    .ThenBy(t => t.DueTime)
                    .Select(t => new
                    {
                        id = t.Id,
                        title = t.Title,
                        description = t.Description,
                        dueDate = t.DueDate,
                        dueTime = t.DueTime,
                        isTimeBased = t.IsTimeBased,
                        status = t.Status,
                        isCompleted = false,
                        groupId = t.GroupId
                    })
                    .ToListAsync();

                // ========================================================
                // DONE TASKS
                // ========================================================
                var doneTasks = await baseQuery
                    .Where(t =>
                        t.Status == "Done")
                    .OrderByDescending(t => t.DueDate)
                    .ThenByDescending(t => t.DueTime)
                    .Select(t => new
                    {
                        id = t.Id,
                        title = t.Title,
                        description = t.Description,
                        dueDate = t.DueDate,
                        dueTime = t.DueTime,
                        isTimeBased = t.IsTimeBased,
                        status = t.Status,
                        isCompleted = true,
                        groupId = t.GroupId
                    })
                    .ToListAsync();

                // ========================================================
                // UPCOMING / TODAY TASKS
                //
                // Incomplete tasks whose due date is today or later.
                //
                // Example:
                // Today      -> included
                // Tomorrow   -> included
                // Next week  -> included
                //
                // Past tasks are not included here.
                // ========================================================
                var upcomingTasks = await baseQuery
                    .Where(t =>
                        t.Status != "Done" &&
                        t.Status != "Cancelled" &&
                        t.DueDate != null &&
                        t.DueDate >= today)
                    .OrderBy(t => t.DueDate)
                    .ThenBy(t => t.DueTime)
                    .Select(t => new
                    {
                        id = t.Id,
                        title = t.Title,
                        description = t.Description,
                        dueDate = t.DueDate,
                        dueTime = t.DueTime,
                        isTimeBased = t.IsTimeBased,
                        status = t.Status,
                        isCompleted = false,
                        groupId = t.GroupId
                    })
                    .ToListAsync();

                // ========================================================
                // RESPONSE
                // ========================================================
                return Ok(new
                {
                    success = true,
                    message = "Task history fetched successfully.",

                    data = new
                    {
                        pending = pendingTasks,
                        done = doneTasks,
                        upcoming = upcomingTasks
                    },

                    counts = new
                    {
                        pending = pendingTasks.Count,
                        done = doneTasks.Count,
                        upcoming = upcomingTasks.Count
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to fetch task history.",
                    error = ex.Message
                });
            }
        }


        [HttpPost("{id}/pick")]
        public async Task<IActionResult> PickTask(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                    return Unauthorized(new { message = "User not authenticated." });

                if (!int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "Invalid user ID." });

                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (task == null)
                    return NotFound(new { message = "Task not found." });

                if (!task.GroupId.HasValue)
                    return BadRequest(new { message = "This is not a group task." });

                if (task.Status != "Pending")
                    return BadRequest(new
                    {
                        message = "This task is no longer available."
                    });

                var member = await _context.GroupMembers
                    .FirstOrDefaultAsync(m =>
                        m.GroupId == task.GroupId.Value &&
                        m.UserId == userId);

                if (member == null)
                    return Forbid();

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return Unauthorized();

                task.AssignedTo = userId;

                var userName = $"{user.FirstName} {user.LastName}".Trim();

                var groupMemberUserIds = await _context.GroupMembers
                    .Where(m =>
                        m.GroupId == task.GroupId.Value &&
                        m.UserId != null &&
                        m.UserId != userId)
                    .Select(m => m.UserId!.Value)
                    .Distinct()
                    .ToListAsync();

                foreach (var receiverId in groupMemberUserIds)
                {
                    _context.Notifications.Add(new Notification
                    {
                        TaskId = task.Id,
                        UserId = receiverId,
                        SenderId = userId,
                        Type = "TaskPicked",
                        Message = $"{userName} picked the task \"{task.Title}\".",
                        IsRead = false,
                        SentAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Task picked successfully.",
                    taskId = task.Id,
                    assignedTo = userId,
                    assignedToName = userName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error while picking task.",
                    error = ex.Message
                });
            }
        }

        [HttpPost("{id}/ignore")]
        public async Task<IActionResult> IgnoreTask(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                    return Unauthorized(new { message = "User not authenticated." });

                if (!int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "Invalid user ID." });

                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (task == null)
                    return NotFound(new { message = "Task not found." });

                if (!task.GroupId.HasValue)
                    return BadRequest(new { message = "This is not a group task." });

                var member = await _context.GroupMembers
                    .FirstOrDefaultAsync(m =>
                        m.GroupId == task.GroupId.Value &&
                        m.UserId == userId);

                if (member == null)
                    return Forbid();

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return Unauthorized();

                var userName = $"{user.FirstName} {user.LastName}".Trim();

                var groupMemberUserIds = await _context.GroupMembers
                    .Where(m =>
                        m.GroupId == task.GroupId.Value &&
                        m.UserId != null &&
                        m.UserId != userId)
                    .Select(m => m.UserId!.Value)
                    .Distinct()
                    .ToListAsync();

                foreach (var receiverId in groupMemberUserIds)
                {
                    _context.Notifications.Add(new Notification
                    {
                        TaskId = task.Id,
                        UserId = receiverId,
                        SenderId = userId,
                        Type = "TaskIgnored",
                        Message = $"{userName} ignored the task \"{task.Title}\".",
                        IsRead = false,
                        SentAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Task ignored successfully.",
                    taskId = task.Id,
                    ignoredBy = userId,
                    ignoredByName = userName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error while ignoring task.",
                    error = ex.Message
                });
            }
        }

        [HttpPost("{taskId}/mention")]
        public async Task<IActionResult> MentionUsers(int taskId, [FromBody] MentionUserRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;

                if (!int.TryParse(userIdClaim, out int currentUserId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                if (request == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Request is required."
                    });
                }

                var mentionedUserIds = (request.MentionedUserIds ?? new List<int>())
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Task not found."
                    });
                }

                if (!task.GroupId.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Only group tasks can contain mentions."
                    });
                }

                int groupId = task.GroupId.Value;

                bool currentUserIsMember = await _context.GroupMembers
                    .AnyAsync(m =>
                        m.GroupId == groupId &&
                        m.UserId == currentUserId);

                if (!currentUserIsMember)
                {
                    return Forbid();
                }

                // No user selected
                if (mentionedUserIds.Count == 0)
                {
                    var existingMentions = await _context.TaskMentions
                        .Where(x => x.TaskId == taskId)
                        .Include(x => x.MentionedUser)
                        .Include(x => x.MentionedByNavigation)
                        .Select(x => new
                        {
                            id = x.Id,
                            taskId = x.TaskId,
                            mentionedUserId = x.MentionedUserId,
                            mentionedUserName =
                                ((x.MentionedUser.FirstName ?? "") + " " +
                                 (x.MentionedUser.LastName ?? "")).Trim(),
                            mentionedBy = x.MentionedBy,
                            mentionedAt = x.MentionedAt
                        })
                        .OrderByDescending(x => x.mentionedAt)
                        .ToListAsync();

                    return Ok(new
                    {
                        success = true,
                        message = "No members were selected for mention.",
                        data = existingMentions
                    });
                }

                // User cannot mention himself
                if (mentionedUserIds.Contains(currentUserId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "You cannot mention yourself."
                    });
                }

                // Get all selected group members
                var validGroupMemberIds = await _context.GroupMembers
                    .Where(m =>
                        m.GroupId == groupId &&
                        m.UserId.HasValue &&
                        mentionedUserIds.Contains(m.UserId.Value))
                    .Select(m => m.UserId!.Value)
                    .Distinct()
                    .ToListAsync();

                // Find users that are not members of this group
                var invalidUserIds = mentionedUserIds
                    .Except(validGroupMemberIds)
                    .ToList();

                if (invalidUserIds.Count > 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "One or more selected users are not members of this group.",
                        invalidUserIds = invalidUserIds
                    });
                }

                // Get already existing mentions
                var alreadyMentionedUserIds = await _context.TaskMentions
                    .Where(x =>
                        x.TaskId == taskId &&
                        mentionedUserIds.Contains(x.MentionedUserId))
                    .Select(x => x.MentionedUserId)
                    .ToListAsync();

                // Only create new mentions
                var newMentionedUserIds = mentionedUserIds
                    .Except(alreadyMentionedUserIds)
                    .ToList();

                var sender = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == currentUserId);

                string senderName =
                    $"{sender?.FirstName ?? ""} {sender?.LastName ?? ""}".Trim();

                if (string.IsNullOrWhiteSpace(senderName))
                {
                    senderName = "A group member";
                }

                string taskTitle = task.Title ?? "a task";

                var mentionedUsers = await _context.Users
                    .Where(u => newMentionedUserIds.Contains(u.Id))
                    .ToListAsync();

                var newMentions = new List<TaskMention>();
                var newNotifications = new List<Notification>();

                foreach (var mentionedUser in mentionedUsers)
                {
                    var mention = new TaskMention
                    {
                        TaskId = taskId,
                        MentionedUserId = mentionedUser.Id,
                        MentionedBy = currentUserId,
                        MentionedAt = DateTime.Now
                    };

                    newMentions.Add(mention);

                    var notification = new Notification
                    {
                        TaskId = taskId,
                        UserId = mentionedUser.Id,
                        SenderId = currentUserId,
                        Type = "Mention",
                        Message = $"{senderName} mentioned you in task \"{taskTitle}\".",
                        IsRead = false,
                        SentAt = DateTime.Now
                    };

                    newNotifications.Add(notification);
                }

                if (newMentions.Count > 0)
                {
                    _context.TaskMentions.AddRange(newMentions);
                }

                if (newNotifications.Count > 0)
                {
                    _context.Notifications.AddRange(newNotifications);
                }

                await _context.SaveChangesAsync();

                var result = newMentions
                    .Select(mention =>
                    {
                        var user = mentionedUsers
                            .FirstOrDefault(u => u.Id == mention.MentionedUserId);

                        return new
                        {
                            id = mention.Id,
                            taskId = mention.TaskId,
                            mentionedUserId = mention.MentionedUserId,
                            mentionedUserName =
                                $"{user?.FirstName ?? ""} {user?.LastName ?? ""}".Trim(),
                            mentionedBy = mention.MentionedBy,
                            mentionedAt = mention.MentionedAt
                        };
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    message = newMentions.Count == 0
                        ? "All selected members were already mentioned."
                        : $"{newMentions.Count} member(s) mentioned successfully.",
                    data = result,
                    alreadyMentionedUserIds = alreadyMentionedUserIds
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        [HttpGet("{taskId}/mentions")]
        public async Task<IActionResult> GetTaskMentions(int taskId)
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;

                if (!int.TryParse(userIdClaim, out int currentUserId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Task not found."
                    });
                }

                if (!task.GroupId.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "This is not a group task."
                    });
                }

                int groupId = task.GroupId.Value;

                bool isMember = await _context.GroupMembers
                    .AnyAsync(m =>
                        m.GroupId == groupId &&
                        m.UserId == currentUserId);

                if (!isMember)
                {
                    return Forbid();
                }

                var mentions = await _context.TaskMentions
                    .Where(x => x.TaskId == taskId)
                    .Include(x => x.MentionedUser)
                    .Include(x => x.MentionedByNavigation)
                    .Select(x => new
                    {
                        id = x.Id,

                        taskId = x.TaskId,

                        mentionedUserId = x.MentionedUserId,

                        mentionedUserName =
                            ((x.MentionedUser.FirstName ?? "") + " " +
                             (x.MentionedUser.LastName ?? "")).Trim(),

                        mentionedBy = x.MentionedBy,

                        mentionedByName =
                            ((x.MentionedByNavigation.FirstName ?? "") + " " +
                             (x.MentionedByNavigation.LastName ?? "")).Trim(),

                        mentionedAt = x.MentionedAt
                    })
                    .OrderByDescending(x => x.mentionedAt)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    count = mentions.Count,
                    data = mentions
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }



        [HttpGet("group/{groupId}/dashboard")]
        public async Task<IActionResult> GetGroupDashboard(int groupId)
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;

                if (!int.TryParse(userIdClaim, out int currentUserId))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid user authentication."
                    });
                }

                var group = await _context.GroupsUsers
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                {
                    return NotFound(new
                    {
                        message = "Group not found."
                    });
                }

                var isMember = await _context.GroupMembers
                    .AnyAsync(gm =>
                        gm.GroupId == groupId &&
                        gm.UserId == currentUserId);

                var isCreator = group.CreatedBy == currentUserId;

                if (!isMember && !isCreator)
                {
                    return Forbid();
                }

                var members = await (
                    from gm in _context.GroupMembers
                    join u in _context.Users
                        on gm.UserId equals u.Id into userJoin
                    from u in userJoin.DefaultIfEmpty()
                    where gm.GroupId == groupId
                    select new
                    {
                        id = gm.Id,
                        userId = gm.UserId,
                        name = u != null
                            ? (u.FirstName + " " + u.LastName).Trim()
                            : gm.Name,
                        phone = u != null
                            ? u.PhoneNumber
                            : gm.Phone,
                        role = gm.Role,
                        isRegistered = gm.UserId != null
                    }
                ).ToListAsync();

                var tasks = await (
                    from t in _context.Tasks
                    join creator in _context.Users
                        on t.CreatedBy equals creator.Id into creatorJoin
                    from creator in creatorJoin.DefaultIfEmpty()

                    join assigned in _context.Users
                        on t.AssignedTo equals assigned.Id into assignedJoin
                    from assigned in assignedJoin.DefaultIfEmpty()

                    where t.GroupId == groupId

                    select new
                    {
                        id = t.Id,
                        title = t.Title,
                        description = t.Description,
                        isTimeBased = t.IsTimeBased,

                        dueDate = t.DueDate,
                        dueTime = t.DueTime,

                        groupId = t.GroupId,

                        createdBy = t.CreatedBy,
                        createdByName = creator != null
                            ? (creator.FirstName + " " + creator.LastName).Trim()
                            : "",

                        assignedTo = t.AssignedTo,
                        assignedToName = assigned != null
                            ? (assigned.FirstName + " " + assigned.LastName).Trim()
                            : "",

                        status = t.Status,

                        latitude = t.Latitude,
                        longitude = t.Longitude,

                        geofenceRadiusMeters =
                            t.GeofenceRadiusMeters,

                        geofenceEnabled =
                            t.GeofenceEnabled,

                        createdAt = t.CreatedAt,
                        updatedAt = t.UpdatedAt
                    }
                )
                .OrderByDescending(t => t.createdAt)
                .ToListAsync();

                var taskIds = tasks
                    .Select(t => t.id)
                    .ToList();

                var mentions = await (
                    from tm in _context.TaskMentions
                    join u in _context.Users
                        on tm.MentionedUserId equals u.Id
                    where taskIds.Contains(tm.TaskId)
                    select new
                    {
                        taskId = tm.TaskId,
                        mentionedUserId = tm.MentionedUserId,
                        mentionedUserName =
                            (u.FirstName + " " + u.LastName).Trim(),
                        mentionedAt = tm.MentionedAt
                    }
                ).ToListAsync();

                var taskResult = tasks.Select(t => new
                {
                    t.id,
                    t.title,
                    t.description,
                    t.isTimeBased,
                    t.dueDate,
                    t.dueTime,
                    t.groupId,
                    t.createdBy,
                    t.createdByName,
                    t.assignedTo,
                    t.assignedToName,
                    t.status,
                    t.latitude,
                    t.longitude,
                    t.geofenceRadiusMeters,
                    t.geofenceEnabled,
                    t.createdAt,
                    t.updatedAt,

                    mentions = mentions
                        .Where(m => m.taskId == t.id)
                        .Select(m => new
                        {
                            m.mentionedUserId,
                            m.mentionedUserName,
                            m.mentionedAt
                        })
                        .ToList()
                }).ToList();

                var pendingCount = taskResult.Count(t =>
                    !string.Equals(
                        t.status,
                        "Done",
                        StringComparison.OrdinalIgnoreCase));

                var completedCount = taskResult.Count(t =>
                    string.Equals(
                        t.status,
                        "Done",
                        StringComparison.OrdinalIgnoreCase));

                return Ok(new
                {
                    groupId = group.Id,
                    groupName = group.Name,

                    totalMembers = members.Count,

                    members = members,

                    totalTasks = taskResult.Count,

                    pendingTasks = pendingCount,

                    completedTasks = completedCount,

                    tasks = taskResult
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "GetGroupDashboard Error: " +
                    ex.ToString());

                return StatusCode(500, new
                {
                    message = "Failed to load group dashboard.",
                    error = ex.Message
                });
            }
        }

    }

    // ================================================================
    // DTO: Snooze Task
    // ================================================================
    public class SnoozeTaskRequest
    {
        public int MinutesToSnooze { get; set; }
    }

    public class MentionUserRequest
    {
        public List<int> MentionedUserIds { get; set; } = new List<int>();
    }
}


















//Working Controller:
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using System.Security.Claims;
//using TODOLISTAPI.Models;

//namespace TODOLISTAPI.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class TaskController : ControllerBase
//    {
//        private readonly TodoSmartAlertsContext _context;

//        public TaskController(TodoSmartAlertsContext context)
//        {
//            _context = context;
//        }

//        // ============================================================
//        // POST: api/Task/{id}/done
//        // Mark task as completed
//        // ============================================================
//        [HttpPost("{id}/done")]
//        public async Task<IActionResult> MarkTaskDone(int id)
//        {
//            try
//            {
//                var task = await _context.Tasks
//                    .FirstOrDefaultAsync(x => x.Id == id);

//                if (task == null)
//                {
//                    return NotFound(new
//                    {
//                        success = false,
//                        message = "Task not found."
//                    });
//                }

//                if (task.Status == "Done")
//                {
//                    return Ok(new
//                    {
//                        success = true,
//                        message = "Task is already marked as done.",
//                        taskId = task.Id,
//                        status = task.Status
//                    });
//                }

//                task.Status = "Done";
//                task.UpdatedAt = DateTime.Now;

//                await _context.SaveChangesAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Task marked as done successfully.",
//                    taskId = task.Id,
//                    status = task.Status
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


//        // ============================================================
//        // GET: api/Task/groups
//        // Get all groups
//        // ============================================================
//        //[HttpGet("groups")]
//        //public async Task<IActionResult> GetGroups()
//        //{
//        //    try
//        //    {
//        //        var groups = await _context.GroupsUsers
//        //            .Select(g => new
//        //            {
//        //                id = g.Id,
//        //                name = g.Name,
//        //                createdBy = g.CreatedBy,
//        //                createdAt = g.CreatedAt
//        //            })
//        //            .ToListAsync();

//        //        return Ok(new
//        //        {
//        //            success = true,
//        //            data = groups
//        //        });
//        //    }
//        //    catch (Exception ex)
//        //    {
//        //        return StatusCode(500, new
//        //        {
//        //            success = false,
//        //            message = ex.Message
//        //        });
//        //    }
//        //}
//        [HttpGet("groups")]
//        public async Task<IActionResult> GetGroups()
//        {
//            try
//            {
//                var groups = await _context.GroupsUsers
//                    .Include(g => g.GroupMembers)
//                    .ThenInclude(m => m.User)
//                    .Select(g => new
//                    {
//                        id = g.Id,

//                        name = g.Name,

//                        createdBy = g.CreatedBy,

//                        createdAt = g.CreatedAt,

//                        members = g.GroupMembers.Select(m => new
//                        {
//                            id = m.Id,

//                            displayName = m.User != null
//                                ? (
//                                    (m.User.FirstName ?? "") +
//                                    " " +
//                                    (m.User.LastName ?? "")
//                                  ).Trim()
//                                : m.Name,

//                            name = m.User != null
//                                ? (
//                                    (m.User.FirstName ?? "") +
//                                    " " +
//                                    (m.User.LastName ?? "")
//                                  ).Trim()
//                                : m.Name,

//                            phone = m.User != null
//                                ? m.User.PhoneNumber
//                                : m.Phone,

//                            role = m.Role,

//                            userId = m.UserId,

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
//                    message = "Failed to load groups.",
//                    error = ex.Message
//                });
//            }
//        }


//        // ============================================================
//        // GET: api/Task/group
//        //
//        // Example:
//        // GET /api/Task/group?groupId=1&isTimeBased=true&tab=today
//        //
//        // Tabs:
//        // today
//        // pending
//        // upcoming
//        // ============================================================
//        [HttpGet("group")]
//        public async Task<IActionResult> GetGroupTasks([FromQuery] string? tab, [FromQuery] bool isTimeBased, [FromQuery] int groupId)
//        {
//            try
//            {
//                var today = DateOnly.FromDateTime(DateTime.Today);

//                IQueryable<Models.Task> query = _context.Tasks
//                    .Where(x =>
//                        x.GroupId == groupId &&
//                        x.IsTimeBased == isTimeBased);

//                switch ((tab ?? "").ToLower())
//                {
//                    case "today":

//                        query = query.Where(x =>
//                            x.DueDate == today);

//                        break;

//                    case "pending":

//                        query = query.Where(x =>
//                            x.Status == "Pending");

//                        break;

//                    case "upcoming":

//                        query = query.Where(x =>
//                            x.DueDate > today);

//                        break;
//                }

//                var tasks = await query
//                    .OrderBy(x => x.DueDate)
//                    .ThenBy(x => x.DueTime)
//                    .Select(x => new
//                    {
//                        id = x.Id,
//                        title = x.Title,
//                        description = x.Description,
//                        dueDate = x.DueDate,
//                        dueTime = x.DueTime,
//                        isTimeBased = x.IsTimeBased,
//                        isCompleted = x.Status == "Done",
//                        status = x.Status,
//                        groupId = x.GroupId,
//                        createdBy = x.CreatedBy,
//                        assignedTo = x.AssignedTo,
//                        createdAt = x.CreatedAt,
//                        updatedAt = x.UpdatedAt
//                    })
//                    .ToListAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Group tasks fetched successfully.",
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


//        // ============================================================
//        // GET: api/Task/personal
//        //
//        // Example:
//        // GET /api/Task/personal?isTimeBased=true&tab=today
//        //
//        // Gets tasks where GroupId is NULL.
//        // ============================================================
//        [HttpGet("personal")]
//        public async Task<IActionResult> GetPersonalTasks([FromQuery] string? tab, [FromQuery] bool isTimeBased)
//        {
//            try
//            {
//                var today = DateOnly.FromDateTime(DateTime.Today);

//                IQueryable<Models.Task> query = _context.Tasks
//                    .Where(t =>
//                        t.GroupId == null &&
//                        t.IsTimeBased == isTimeBased);

//                switch ((tab ?? "").ToLower())
//                {
//                    case "today":

//                        query = query.Where(t =>
//                            t.DueDate == today);

//                        break;

//                    case "pending":

//                        query = query.Where(t =>
//                            t.Status == "Pending");

//                        break;

//                    case "upcoming":

//                        query = query.Where(t =>
//                            t.DueDate > today);

//                        break;
//                }

//                var tasks = await query
//                    .OrderBy(t => t.DueDate)
//                    .ThenBy(t => t.DueTime)
//                    .Select(t => new
//                    {
//                        id = t.Id,
//                        title = t.Title,
//                        description = t.Description,
//                        dueDate = t.DueDate,
//                        dueTime = t.DueTime,
//                        isTimeBased = t.IsTimeBased,
//                        status = t.Status,
//                        isCompleted = t.Status == "Done",
//                        groupId = t.GroupId,
//                        createdBy = t.CreatedBy,
//                        assignedTo = t.AssignedTo,
//                        createdAt = t.CreatedAt,
//                        updatedAt = t.UpdatedAt
//                    })
//                    .ToListAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Personal tasks fetched successfully.",
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


//        // ============================================================
//        // GET: api/Task
//        //
//        // Example:
//        // GET /api/Task?isTimeBased=true
//        //
//        // Gets all tasks.
//        // ============================================================
//        [HttpGet]
//        public async Task<IActionResult> GetTasks([FromQuery] bool isTimeBased)
//        {
//            try
//            {
//                var tasks = await _context.Tasks
//                    .Where(t => t.IsTimeBased == isTimeBased)
//                    .OrderBy(t => t.Status == "Done")
//                    .ThenBy(t => t.DueDate)
//                    .ThenBy(t => t.DueTime)
//                    .Select(t => new
//                    {
//                        id = t.Id,
//                        title = t.Title,
//                        description = t.Description,
//                        dueDate = t.DueDate,
//                        dueTime = t.DueTime,
//                        isTimeBased = t.IsTimeBased,
//                        status = t.Status,
//                        isCompleted = t.Status == "Done",
//                        groupId = t.GroupId,
//                        createdBy = t.CreatedBy,
//                        assignedTo = t.AssignedTo,
//                        createdAt = t.CreatedAt,
//                        updatedAt = t.UpdatedAt
//                    })
//                    .ToListAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Tasks fetched successfully.",
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


//        // ============================================================
//        // DELETE: api/Task/task/{id}
//        // Delete a task
//        // ============================================================
//        [HttpDelete("task/{id}")]
//        public async Task<IActionResult> DeleteTask(int id)
//        {
//            try
//            {
//                var task = await _context.Tasks
//                    .FindAsync(id);

//                if (task == null)
//                {
//                    return NotFound(new
//                    {
//                        success = false,
//                        message = "Task not found."
//                    });
//                }

//                _context.Tasks.Remove(task);

//                await _context.SaveChangesAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Task deleted successfully.",
//                    taskId = id
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


//        // ============================================================
//        // DELETE: api/Task/group/{id}
//        // Delete a group and its tasks/members
//        // ============================================================
//        [HttpDelete("group/{id}")]
//        public async Task<IActionResult> DeleteGroup(int id)
//        {
//            try
//            {
//                var group = await _context.GroupsUsers
//                    .Include(g => g.Tasks)
//                    .Include(g => g.GroupMembers)
//                    .FirstOrDefaultAsync(g => g.Id == id);

//                if (group == null)
//                {
//                    return NotFound(new
//                    {
//                        success = false,
//                        message = "Group not found."
//                    });
//                }

//                // Delete tasks belonging to the group
//                if (group.Tasks != null && group.Tasks.Any())
//                {
//                    _context.Tasks.RemoveRange(group.Tasks);
//                }

//                // Delete group members
//                if (group.GroupMembers != null && group.GroupMembers.Any())
//                {
//                    _context.GroupMembers.RemoveRange(group.GroupMembers);
//                }

//                // Delete group
//                _context.GroupsUsers.Remove(group);

//                await _context.SaveChangesAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Group deleted successfully.",
//                    groupId = id
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


//        // ============================================================
//        // POST: api/Task/{id}/snooze
//        // Snooze a task
//        // ============================================================
//        [HttpPost("{id}/snooze")]
//        public async Task<IActionResult> SnoozeTask(int id, [FromBody] SnoozeTaskRequest request)
//        {
//            try
//            {
//                // Validate request
//                if (request == null)
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "Request body is required."
//                    });
//                }

//                if (request.MinutesToSnooze <= 0)
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "MinutesToSnooze must be greater than zero."
//                    });
//                }

//                // Find task
//                var task = await _context.Tasks
//                    .FirstOrDefaultAsync(x => x.Id == id);

//                if (task == null)
//                {
//                    return NotFound(new
//                    {
//                        success = false,
//                        message = "Task not found."
//                    });
//                }

//                // Completed task cannot be snoozed
//                if (task.Status == "Done")
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "Completed task cannot be snoozed."
//                    });
//                }

//                // Task must have due date
//                if (task.DueDate == null)
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "Task has no due date."
//                    });
//                }

//                // Task must have due time
//                if (task.DueTime == null)
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "Task has no due time."
//                    });
//                }

//                // Current due date/time
//                DateTime currentDueDateTime =
//                    task.DueDate.Value.ToDateTime(task.DueTime.Value);

//                // Calculate new wake time
//                DateTime newWakeTime =
//                    currentDueDateTime.AddMinutes(
//                        request.MinutesToSnooze);

//                // Update task
//                task.DueDate =
//                    DateOnly.FromDateTime(newWakeTime);

//                task.DueTime =
//                    TimeOnly.FromDateTime(newWakeTime);

//                task.Status = "Snoozed";

//                task.UpdatedAt = DateTime.Now;

//                // Create snooze history
//                var snoozeLog = new SnoozeLog
//                {
//                    TaskId = task.Id,
//                    UserId = task.CreatedBy,
//                    SnoozedAt = DateTime.Now,
//                    WakeAt = newWakeTime
//                };

//                _context.SnoozeLogs.Add(snoozeLog);

//                await _context.SaveChangesAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message =
//                        $"Task snoozed for {request.MinutesToSnooze} minutes.",
//                    taskId = task.Id,
//                    wakeAt = newWakeTime,
//                    dueDate = task.DueDate,
//                    dueTime = task.DueTime,
//                    status = task.Status
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

//        // ============================================================
//        // GET api/Task/clashes
//        // Returns pairs of the user's own (not-done, not-cancelled) tasks
//        // that share the same DueDate + DueTime — i.e. scheduling conflicts.
//        // "Own" = assigned to the user, or created by them if unassigned.
//        // ============================================================
//        [HttpGet("clashes")]
//        public async Task<IActionResult> GetClashes()
//        {
//            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

//            var tasks = await _context.Tasks
//                .Include(t => t.Group)
//                .Where(t =>
//                    (t.AssignedTo == userId || (t.AssignedTo == null && t.CreatedBy == userId)) &&
//                    t.Status != "Done" &&
//                    t.Status != "Cancelled" &&
//                    t.DueDate != null)
//                .OrderBy(t => t.DueDate)
//                .ThenBy(t => t.DueTime)
//                .ToListAsync();

//            var clashGroups = tasks
//                .GroupBy(t => new { t.DueDate, t.DueTime })
//                .Where(g => g.Count() > 1)
//                .ToList();

//            var result = clashGroups.Select(g => new
//            {
//                dueDate = g.Key.DueDate,
//                dueTime = g.Key.DueTime,
//                tasks = g.Select(t => new
//                {
//                    id = t.Id,
//                    title = t.Title,
//                    dueDate = t.DueDate,
//                    dueTime = t.DueTime,
//                    groupName = t.Group != null ? t.Group.Name : null
//                })
//            });

//            return Ok(result);
//        }

//        // ============================================================
//        // POST api/Task/{id}/done
//        // Marks a single task as done. Matches the frontend call:
//        //   fetch(`${BASE_URL}/Task/${taskId}/done`, { method: 'POST' })
//        // ============================================================
//        [HttpPost("{id}/done")]
//        public async Task<IActionResult> MarkDone(int id)
//        {
//            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

//            var task = await _context.Tasks
//                .FirstOrDefaultAsync(t => t.Id == id &&
//                    (t.AssignedTo == userId || (t.AssignedTo == null && t.CreatedBy == userId)));

//            if (task is null)
//            {
//                return NotFound(new { message = "Task not found." });
//            }

//            if (task.Status == "Done")
//            {
//                return Ok(new { success = true, message = "Task already marked done." });
//            }

//            task.Status = "Done";
//            // No need to set UpdatedAt manually — trg_Tasks_UpdatedAt handles that in the DB.

//            await _context.SaveChangesAsync();

//            return Ok(new { success = true, id = task.Id, status = task.Status });
//        }
//    }



//        // ================================================================
//        // DTO: Snooze Task
//        // ================================================================
//        public class SnoozeTaskRequest
//    {
//        public int MinutesToSnooze { get; set; }
//    }
//}












////using Microsoft.AspNetCore.Http;
////using Microsoft.AspNetCore.Mvc;
////using TODOLISTAPI.Models;
////using Microsoft.EntityFrameworkCore;

////namespace TODOLISTAPI.Controllers
////{
////    [Route("api/[controller]")]
////    [ApiController]
////    public class TaskController : ControllerBase
////    {
////        private readonly TodoSmartAlertsContext _context;

////        public TaskController(TodoSmartAlertsContext context)
////        {
////            _context = context;
////        }

////        //====================================================
////        // POST: api/tasks/{id}/done
////        //====================================================
////        [HttpPost("{id}/done")]
////        public async Task<IActionResult> MarkTaskDone(int id)
////        {
////            try
////            {
////                // Find task
////                var task = await _context.Tasks
////                    .FirstOrDefaultAsync(x => x.Id == id);

////                if (task == null)
////                {
////                    return NotFound(new
////                    {
////                        success = false,
////                        message = "Task not found."
////                    });
////                }

////                // Already completed
////                if (task.Status == "Done")
////                {
////                    return Ok(new
////                    {
////                        success = true,
////                        message = "Task is already marked as done."
////                    });
////                }

////                // Update Status
////                task.Status = "Done";

////                // UpdatedAt trigger exists,
////                // but keeping this is also fine.
////                task.UpdatedAt = DateTime.Now;

////                _context.Tasks.Update(task);

////                await _context.SaveChangesAsync();

////                return Ok(new
////                {
////                    success = true,
////                    message = "Task marked as done successfully.",
////                    taskId = task.Id,
////                    status = task.Status
////                });
////            }
////            catch (Exception ex)
////            {
////                return StatusCode(500, new
////                {
////                    success = false,
////                    message = ex.Message
////                });
////            }
////        }

////        [HttpGet]
////        public async Task<IActionResult> GetGroups()
////        {
////            var groups = await _context.GroupsUsers
////                .Select(g => new
////                {
////                    id = g.Id,
////                    name = g.Name,
////                    createdBy = g.CreatedBy,
////                    createdAt = g.CreatedAt
////                })
////                .ToListAsync();

////            return Ok(new
////            {
////                success = true,
////                data = groups
////            });
////        }

////        [HttpGet]
////        public async Task<IActionResult> GetTasks(
////            string? tab,
////            bool isTimeBased,
////            int groupId)
////        {
////            var today = DateOnly.FromDateTime(DateTime.Today);

////            IQueryable<Models.Task> query = _context.Tasks
////                .Where(x =>
////                    x.GroupId == groupId &&
////                    x.IsTimeBased == isTimeBased);

////            switch ((tab ?? "").ToLower())
////            {
////                case "today":

////                    query = query.Where(x => x.DueDate == today);

////                    break;

////                case "pending":

////                    query = query.Where(x => x.Status == "Pending");

////                    break;

////                case "upcoming":

////                    query = query.Where(x => x.DueDate > today);

////                    break;
////            }

////            var tasks = await query
////                .OrderBy(x => x.DueDate)
////                .ThenBy(x => x.DueTime)
////                .Select(x => new
////                {
////                    id = x.Id,
////                    title = x.Title,
////                    description = x.Description,
////                    dueDate = x.DueDate,
////                    dueTime = x.DueTime,
////                    isCompleted = x.Status == "Done",
////                    status = x.Status,
////                    groupId = x.GroupId
////                })
////                .ToListAsync();

////            return Ok(new
////            {
////                success = true,
////                data = tasks
////            });
////        }

////        //====================================================
////        // DELETE api/tasks/{id}
////        //====================================================
////        [HttpDelete("{id}")]
////        public async Task<IActionResult> DeleteTask(int id)
////        {
////            var task = await _context.Tasks.FindAsync(id);

////            if (task == null)
////            {
////                return NotFound(new
////                {
////                    success = false,
////                    message = "Task not found."
////                });
////            }

////            _context.Tasks.Remove(task);

////            await _context.SaveChangesAsync();

////            return Ok(new
////            {
////                success = true,
////                message = "Task deleted successfully."
////            });
////        }

////        //====================================================
////        // DELETE api/groups/{id}
////        //====================================================
////        [HttpDelete("{id}")]
////        public async Task<IActionResult> DeleteGroup(int id)
////        {
////            var group = await _context.GroupsUsers
////                .Include(g => g.Tasks)
////                .Include(g => g.GroupMembers)
////                .FirstOrDefaultAsync(g => g.Id == id);

////            if (group == null)
////            {
////                return NotFound(new
////                {
////                    success = false,
////                    message = "Group not found."
////                });
////            }

////            // Delete tasks belonging to the group
////            if (group.Tasks.Any())
////                _context.Tasks.RemoveRange(group.Tasks);

////            // Delete members
////            if (group.GroupMembers.Any())
////                _context.GroupMembers.RemoveRange(group.GroupMembers);

////            // Delete group
////            _context.GroupsUsers.Remove(group);

////            await _context.SaveChangesAsync();

////            return Ok(new
////            {
////                success = true,
////                message = "Group deleted successfully."
////            });
////        }

////        [HttpGet]
////        public async Task<IActionResult> GetTasks(
////            string? tab,
////            bool isTimeBased)
////        {
////            DateOnly today = DateOnly.FromDateTime(DateTime.Today);

////            IQueryable<Models.Task> query = _context.Tasks
////                .Where(t =>
////                    t.GroupId == null &&
////                    t.IsTimeBased == isTimeBased);

////            switch ((tab ?? "").ToLower())
////            {
////                case "today":

////                    query = query.Where(t =>
////                        t.DueDate == today);

////                    break;

////                case "pending":

////                    query = query.Where(t =>
////                        t.Status == "Pending");

////                    break;

////                case "upcoming":

////                    query = query.Where(t =>
////                        t.DueDate > today);

////                    break;
////            }

////            var tasks = await query
////                .OrderBy(t => t.DueDate)
////                .ThenBy(t => t.DueTime)
////                .Select(t => new
////                {
////                    id = t.Id,
////                    title = t.Title,
////                    description = t.Description,

////                    dueDate = t.DueDate,

////                    dueTime = t.DueTime,

////                    isTimeBased = t.IsTimeBased,

////                    status = t.Status,

////                    isCompleted = t.Status == "Done",

////                    createdAt = t.CreatedAt,

////                    updatedAt = t.UpdatedAt
////                })
////                .ToListAsync();

////            return Ok(new
////            {
////                success = true,
////                data = tasks
////            });
////        }


////        [HttpGet]
////        public async Task<IActionResult> GetTasks(bool isTimeBased)
////        {
////            try
////            {
////                var tasks = await _context.Tasks
////                    .Where(t => t.IsTimeBased == isTimeBased)
////                    .OrderBy(t => t.Status == "Done")
////                    .ThenBy(t => t.DueDate)
////                    .ThenBy(t => t.DueTime)
////                    .Select(t => new
////                    {
////                        id = t.Id,

////                        title = t.Title,

////                        description = t.Description,

////                        dueDate = t.DueDate,

////                        dueTime = t.DueTime,

////                        isTimeBased = t.IsTimeBased,

////                        status = t.Status,

////                        isCompleted = t.Status == "Done",

////                        groupId = t.GroupId,

////                        createdBy = t.CreatedBy,

////                        assignedTo = t.AssignedTo,

////                        createdAt = t.CreatedAt,

////                        updatedAt = t.UpdatedAt
////                    })
////                    .ToListAsync();

////                return Ok(new
////                {
////                    success = true,
////                    message = "Tasks fetched successfully.",
////                    data = tasks
////                });
////            }
////            catch (Exception ex)
////            {
////                return StatusCode(500, new
////                {
////                    success = false,
////                    message = ex.Message
////                });
////            }
////        }

////        [HttpPost("{id}/snooze")]
////        public async Task<IActionResult> SnoozeTask(
////            int id,
////            [FromBody] SnoozeTaskRequest request)
////        {
////            try
////            {
////                var task = await _context.Tasks
////                    .FirstOrDefaultAsync(x => x.Id == id);

////                if (task == null)
////                {
////                    return NotFound(new
////                    {
////                        success = false,
////                        message = "Task not found."
////                    });
////                }

////                if (task.Status == "Done")
////                {
////                    return BadRequest(new
////                    {
////                        success = false,
////                        message = "Completed task cannot be snoozed."
////                    });
////                }

////                if (task.DueDate == null || task.DueTime == null)
////                {
////                    return BadRequest(new
////                    {
////                        success = false,
////                        message = "Task has no due date or time."
////                    });
////                }

////                DateTime currentDueDateTime =
////                    task.DueDate.Value.ToDateTime(task.DueTime.Value);

////                DateTime newWakeTime =
////                    currentDueDateTime.AddMinutes(request.MinutesToSnooze);

////                // Update task
////                task.DueDate = DateOnly.FromDateTime(newWakeTime);
////                task.DueTime = TimeOnly.FromDateTime(newWakeTime);
////                task.Status = "Snoozed";

////                // Save snooze history
////                var snoozeLog = new SnoozeLog
////                {
////                    TaskId = task.Id,
////                    UserId = task.CreatedBy,
////                    SnoozedAt = DateTime.Now,
////                    WakeAt = newWakeTime
////                };

////                _context.SnoozeLogs.Add(snoozeLog);

////                await _context.SaveChangesAsync();

////                return Ok(new
////                {
////                    success = true,
////                    message = $"Task snoozed for {request.MinutesToSnooze} minutes.",
////                    wakeAt = newWakeTime
////                });
////            }
////            catch (Exception ex)
////            {
////                return StatusCode(500, new
////                {
////                    success = false,
////                    message = ex.Message
////                });
////            }
////        }


////    }
////    public class SnoozeTaskRequest
////    {
////        public int MinutesToSnooze { get; set; }
////    }

////}