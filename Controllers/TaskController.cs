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

                        groupId = t.GroupId
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
    }

    // ================================================================
    // DTO: Snooze Task
    // ================================================================
    public class SnoozeTaskRequest
    {
        public int MinutesToSnooze { get; set; }
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
