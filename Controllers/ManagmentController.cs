using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TODOLISTAPI.Models;

namespace TODOLISTAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ManagmentController : ControllerBase
    {
        private readonly TodoSmartAlertsContext _context;

        public ManagmentController(TodoSmartAlertsContext context)
        {
            _context = context;
        }

        // ============================================================
        // GET CURRENT USER ID FROM JWT
        // ============================================================
        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;

            // First try "UserId"
            var userIdClaim = User.FindFirst("UserId")?.Value;

            // Then try standard NameIdentifier claim
            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }

            return int.TryParse(userIdClaim, out userId);
        }


        // ============================================================
        // ADD MEMBER TO GROUP
        // POST: api/Managment/{groupId}/members
        // ============================================================
        [HttpPost("{groupId}/members")]
        public async Task<IActionResult> AddMember(int groupId, [FromBody] AddGroupMemberDto model)
        {
            try
            {
                // ----------------------------------------------------
                // Validate JWT
                // ----------------------------------------------------
                if (!TryGetCurrentUserId(out int loggedInUserId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                // ----------------------------------------------------
                // Validate request
                // ----------------------------------------------------
                if (model == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Request body is required."
                    });
                }

                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Member name is required."
                    });
                }

                if (string.IsNullOrWhiteSpace(model.Phone))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Phone number is required."
                    });
                }

                // ----------------------------------------------------
                // Find group
                // ----------------------------------------------------
                var group = await _context.GroupsUsers
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Group not found."
                    });
                }

                // ----------------------------------------------------
                // Check group ownership
                // ----------------------------------------------------
                if (group.CreatedBy != loggedInUserId)
                {
                    return Forbid();
                }

                // ----------------------------------------------------
                // Prevent duplicate phone number in same group
                // ----------------------------------------------------
                string phone = model.Phone.Trim();

                bool phoneExists = await _context.GroupMembers
                    .AnyAsync(x =>
                        x.GroupId == groupId &&
                        x.Phone == phone);

                if (phoneExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "This phone number already exists in the selected group."
                    });
                }

                // ----------------------------------------------------
                // If registered user ID supplied, verify user exists
                // ----------------------------------------------------
                if (model.UserId.HasValue)
                {
                    bool userExists = await _context.Users
                        .AnyAsync(x => x.Id == model.UserId.Value);

                    if (!userExists)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Selected user does not exist."
                        });
                    }

                    // Prevent same registered user from being added twice
                    bool alreadyMember = await _context.GroupMembers
                        .AnyAsync(x =>
                            x.GroupId == groupId &&
                            x.UserId == model.UserId.Value);

                    if (alreadyMember)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "This user is already a member of the group."
                        });
                    }
                }

                // ----------------------------------------------------
                // Create member
                // ----------------------------------------------------
                var member = new GroupMember
                {
                    GroupId = groupId,
                    UserId = model.UserId,
                    Name = model.Name.Trim(),
                    Phone = phone,
                    Role = "Member",
                    AddedAt = DateTime.Now
                };

                _context.GroupMembers.Add(member);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Member added successfully.",
                    data = new
                    {
                        id = member.Id,
                        groupId = member.GroupId,
                        userId = member.UserId,
                        name = member.Name,
                        phone = member.Phone,
                        role = member.Role,
                        isRegistered = member.UserId != null
                    }
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
        // CREATE TASK
        // POST: api/Managment/task
        // ============================================================
        [HttpPost("task")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto model)
        {
            try
            {
                // ----------------------------------------------------
                // Validate request
                // ----------------------------------------------------
                if (model == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Request body is required."
                    });
                }

                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Title is required."
                    });
                }

                if (string.IsNullOrWhiteSpace(model.Description))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Description is required."
                    });
                }

                // ----------------------------------------------------
                // Get logged-in user
                // ----------------------------------------------------
                if (!TryGetCurrentUserId(out int loggedInUserId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                // ----------------------------------------------------
                // Validate group if supplied
                // ----------------------------------------------------
                if (model.GroupId.HasValue)
                {
                    bool groupExists = await _context.GroupsUsers
                        .AnyAsync(g => g.Id == model.GroupId.Value);

                    if (!groupExists)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Selected group does not exist."
                        });
                    }
                }

                // ----------------------------------------------------
                // If task is not time-based, remove DueTime
                // ----------------------------------------------------
                TimeOnly? dueTime = model.IsTimeBased
                    ? model.DueTime
                    : null;

                // ----------------------------------------------------
                // Create task
                // ----------------------------------------------------
                var task = new Models.Task
                {
                    Title = model.Title.Trim(),
                    Description = model.Description.Trim(),

                    IsTimeBased = model.IsTimeBased,

                    DueDate = model.DueDate,
                    DueTime = dueTime,

                    GroupId = model.GroupId,

                    // Location based / place reminder.
                    // Defaults match the database defaults (200 m, enabled).
                    Latitude = model.Latitude,
                    Longitude = model.Longitude,
                    GeofenceRadiusMeters = model.GeofenceRadiusMeters ?? 200,
                    GeofenceEnabled = model.GeofenceEnabled ?? true,

                    CreatedBy = loggedInUserId,
                    AssignedTo = null,

                    Status = "Pending",

                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Tasks.Add(task);

                await _context.SaveChangesAsync();

                // =====================================================
                // NOTIFICATIONS: "created the task"
                //
                // Group task -> notify the other group members so it
                // shows in their NotificationScreen AND the group
                // activity feed. Personal task -> nobody else is
                // involved.
                // =====================================================
                if (task.GroupId != null)
                {
                    var creator = await _context.Users
                        .FirstOrDefaultAsync(u => u.Id == loggedInUserId);

                    var creatorName = creator != null
                        ? ($"{creator.FirstName} {creator.LastName}").Trim()
                        : "Someone";

                    var groupName = await _context.GroupsUsers
                        .Where(g => g.Id == task.GroupId.Value)
                        .Select(g => g.Name)
                        .FirstOrDefaultAsync();

                    var memberIds = await _context.GroupMembers
                        .Where(m =>
                            m.GroupId == task.GroupId.Value &&
                            m.UserId != null &&
                            m.UserId != loggedInUserId)
                        .Select(m => m.UserId!.Value)
                        .Distinct()
                        .ToListAsync();

                    foreach (var receiverId in memberIds)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            TaskId = task.Id,
                            UserId = receiverId,
                            SenderId = loggedInUserId,
                            Type = "TaskCreated",
                            Message =
                                $"{creatorName} created the task \"{task.Title}\"" +
                                (string.IsNullOrWhiteSpace(groupName)
                                    ? "."
                                    : $" in group \"{groupName}\"."),
                            IsRead = false,
                            SentAt = DateTime.Now
                        });
                    }

                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    success = true,
                    message = "Task has been created successfully.",
                    data = new
                    {
                        id = task.Id,
                        title = task.Title,
                        description = task.Description,
                        dueDate = task.DueDate,
                        dueTime = task.DueTime,
                        isTimeBased = task.IsTimeBased,
                        groupId = task.GroupId,
                        status = task.Status,
                        createdBy = task.CreatedBy,
                        assignedTo = task.AssignedTo,
                        createdAt = task.CreatedAt,
                        updatedAt = task.UpdatedAt
                    }
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
        // CREATE GROUP
        // POST: api/Managment/group
        // ============================================================
        [HttpPost("group")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Request body is required."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Group name is required."
                });
            }

            // --------------------------------------------------------
            // Get current user
            // --------------------------------------------------------
            if (!TryGetCurrentUserId(out int currentUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid token."
                });
            }

            using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // ----------------------------------------------------
                // Create Group
                // ----------------------------------------------------
                var group = new GroupsUser
                {
                    Name = request.Name.Trim(),
                    CreatedBy = currentUserId,
                    CreatedAt = DateTime.Now
                };

                _context.GroupsUsers.Add(group);

                await _context.SaveChangesAsync();

                // ----------------------------------------------------
                // Add creator as Admin
                // ----------------------------------------------------
                var adminMember = new GroupMember
                {
                    GroupId = group.Id,
                    UserId = currentUserId,
                    Role = "Admin",
                    AddedAt = DateTime.Now
                };

                _context.GroupMembers.Add(adminMember);

                // ----------------------------------------------------
                // Add registered users
                // ----------------------------------------------------
                if (request.MemberUserIds != null &&
                    request.MemberUserIds.Any())
                {
                    foreach (var memberId in request.MemberUserIds.Distinct())
                    {
                        // Don't add creator twice
                        if (memberId == currentUserId)
                            continue;

                        // Check user exists
                        bool userExists = await _context.Users
                            .AnyAsync(x => x.Id == memberId);

                        if (!userExists)
                            continue;

                        // Check if already member
                        bool alreadyMember =
                            await _context.GroupMembers.AnyAsync(x =>
                                x.GroupId == group.Id &&
                                x.UserId == memberId);

                        if (alreadyMember)
                            continue;

                        _context.GroupMembers.Add(new GroupMember
                        {
                            GroupId = group.Id,
                            UserId = memberId,
                            Role = "Member",
                            AddedAt = DateTime.Now
                        });
                    }
                }

                // ----------------------------------------------------
                // Add external members
                // ----------------------------------------------------
                if (request.ExternalMembers != null &&
                    request.ExternalMembers.Any())
                {
                    foreach (var member in request.ExternalMembers)
                    {
                        if (member == null ||
                            string.IsNullOrWhiteSpace(member.Name))
                        {
                            continue;
                        }

                        _context.GroupMembers.Add(new GroupMember
                        {
                            GroupId = group.Id,
                            UserId = null,
                            Name = member.Name.Trim(),
                            Phone = string.IsNullOrWhiteSpace(member.Phone)
                                ? null
                                : member.Phone.Trim(),
                            Role = "Member",
                            AddedAt = DateTime.Now
                        });
                    }
                }

                // ----------------------------------------------------
                // Save members
                // ----------------------------------------------------
                await _context.SaveChangesAsync();

                // ----------------------------------------------------
                // Commit
                // ----------------------------------------------------
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Group created successfully.",
                    data = new
                    {
                        groupId = group.Id,
                        groupName = group.Name,
                        createdBy = group.CreatedBy,
                        createdAt = group.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // ============================================================
        // UPDATE TASK
        // PUT: api/Managment/{id}
        //
        // IMPORTANT:
        // There is ONLY ONE UpdateTask method now.
        // It supports both time-based and non-time-based tasks.
        // ============================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskRequest request)
        {
            try
            {
                // ----------------------------------------------------
                // Validate request
                // ----------------------------------------------------
                if (request == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Request body is required."
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Title is required."
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Description))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Description is required."
                    });
                }

                // ----------------------------------------------------
                // Get logged-in user
                // ----------------------------------------------------
                if (!TryGetCurrentUserId(out int currentUserId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid token."
                    });
                }

                // ----------------------------------------------------
                // Find task belonging to current user
                // ----------------------------------------------------
                var task = await _context.Tasks
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        x.CreatedBy == currentUserId);

                if (task == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Task not found or access denied."
                    });
                }

                // ----------------------------------------------------
                // Update task
                // ----------------------------------------------------
                task.Title = request.Title.Trim();

                task.Description = request.Description.Trim();

                task.DueDate = request.DueDate;

                task.IsTimeBased = request.IsTimeBased;

                // Place: only overwrite what the client actually sent.
                if (request.Latitude.HasValue)
                {
                    task.Latitude = request.Latitude;
                }

                if (request.Longitude.HasValue)
                {
                    task.Longitude = request.Longitude;
                }

                if (request.GeofenceRadiusMeters.HasValue)
                {
                    task.GeofenceRadiusMeters = request.GeofenceRadiusMeters.Value;
                }

                if (request.GeofenceEnabled.HasValue)
                {
                    task.GeofenceEnabled = request.GeofenceEnabled.Value;
                }

                // ----------------------------------------------------
                // Time-based task
                // ----------------------------------------------------
                if (request.IsTimeBased)
                {
                    task.DueTime = request.DueTime;
                }
                else
                {
                    // Non-time-based task should not have a time
                    task.DueTime = null;
                }

                task.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Task updated successfully.",
                    data = new
                    {
                        id = task.Id,
                        title = task.Title,
                        description = task.Description,
                        dueDate = task.DueDate,
                        dueTime = task.DueTime,
                        isTimeBased = task.IsTimeBased,
                        groupId = task.GroupId,
                        status = task.Status,
                        createdBy = task.CreatedBy,
                        assignedTo = task.AssignedTo,
                        updatedAt = task.UpdatedAt
                    }
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
        // FORWARD TASK
        // POST: api/Managment/{taskId}/forward
        // ============================================================
        [HttpPost("{taskId}/forward")]
        public async Task<IActionResult> ForwardTask(int taskId, [FromBody] ForwardTaskRequest request)
        {
            try
            {
                // ----------------------------------------------------
                // Validate request
                // ----------------------------------------------------
                if (request == null ||
                    request.ToUserIds == null ||
                    request.ToUserIds.Count == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Please select at least one user."
                    });
                }

                // ----------------------------------------------------
                // Get logged-in user
                // ----------------------------------------------------
                if (!TryGetCurrentUserId(out int currentUserId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid token."
                    });
                }

                // ----------------------------------------------------
                // Find task
                // ----------------------------------------------------
                var task = await _context.Tasks
                    .FirstOrDefaultAsync(x =>
                        x.Id == taskId &&
                        x.CreatedBy == currentUserId);

                if (task == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Task not found or access denied."
                    });
                }

                using var transaction =
                    await _context.Database.BeginTransactionAsync();

                try
                {
                    int forwardedCount = 0;

                    foreach (int userId in request.ToUserIds.Distinct())
                    {
                        // ------------------------------------------------
                        // Don't forward to yourself
                        // ------------------------------------------------
                        if (userId == currentUserId)
                            continue;

                        // ------------------------------------------------
                        // Check user exists
                        // ------------------------------------------------
                        bool userExists = await _context.Users
                            .AnyAsync(x => x.Id == userId);

                        if (!userExists)
                            continue;

                        // ------------------------------------------------
                        // Check if already forwarded
                        // ------------------------------------------------
                        bool alreadyForwarded =
                            await _context.ForwardedTasks.AnyAsync(x =>
                                x.OriginalTaskId == taskId &&
                                x.ForwardedTo == userId);

                        if (alreadyForwarded)
                            continue;

                        // ------------------------------------------------
                        // Create forwarded task record
                        // ------------------------------------------------
                        _context.ForwardedTasks.Add(
                            new ForwardedTask
                            {
                                OriginalTaskId = taskId,
                                ForwardedBy = currentUserId,
                                ForwardedTo = userId,
                                ForwardedAt = DateTime.Now
                            });

                        // ------------------------------------------------
                        // Create notification
                        // ------------------------------------------------
                        _context.Notifications.Add(
                            new Notification
                            {
                                TaskId = taskId,
                                UserId = userId,
                                Message =
                                    $"A task \"{task.Title}\" has been forwarded to you.",
                                IsRead = false,
                                SentAt = DateTime.Now
                            });

                        forwardedCount++;
                    }

                    // ------------------------------------------------
                    // Save
                    // ------------------------------------------------
                    await _context.SaveChangesAsync();

                    // ------------------------------------------------
                    // Commit
                    // ------------------------------------------------
                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        success = true,
                        message =
                            $"{forwardedCount} task(s) forwarded successfully."
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    return StatusCode(500, new
                    {
                        success = false,
                        message = ex.Message
                    });
                }
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

        [HttpGet("group/{groupId}/members")]
        public async Task<IActionResult> GetGroupMembers(int groupId)
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

                var group = await _context.GroupsUsers
                    .FirstOrDefaultAsync(g =>
                        g.Id == groupId &&
                        (
                            g.CreatedBy == currentUserId ||
                            g.GroupMembers.Any(m => m.UserId == currentUserId)
                        ));

                if (group == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Group not found or you are not a member of this group."
                    });
                }

                var members = await _context.GroupMembers
                    .Where(m => m.GroupId == groupId)
                    .Include(m => m.User)
                    .Select(m => new
                    {
                        id = m.Id,
                        userId = m.UserId,

                        name = m.UserId != null
                            ? ((m.User.FirstName ?? "") + " " + (m.User.LastName ?? "")).Trim()
                            : (m.Name ?? "External Member"),

                        firstName = m.UserId != null
                            ? m.User.FirstName
                            : m.Name,

                        lastName = m.UserId != null
                            ? m.User.LastName
                            : "",

                        email = m.UserId != null
                            ? m.User.Email
                            : null,

                        phone = m.UserId != null
                            ? m.User.PhoneNumber
                            : m.Phone,

                        role = m.Role,

                        isRegistered = m.UserId != null,

                        addedAt = m.AddedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,

                    data = new
                    {
                        groupId = group.Id,
                        groupName = group.Name,
                        totalMembers = members.Count,
                        members = members
                    }
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

    }


    // ================================================================
    // DTOs
    // ================================================================

    public class AddGroupMemberDto
    {
        public int? UserId { get; set; }

        public string Name { get; set; }

        public string Phone { get; set; }
    }


    // ================================================================
    // CREATE TASK DTO
    // ================================================================
    public class CreateTaskDto
    {
        public string Title { get; set; }

        public string Description { get; set; }

        public DateOnly? DueDate { get; set; }

        public TimeOnly? DueTime { get; set; }

        public bool IsTimeBased { get; set; }

        public int? GroupId { get; set; }

        // ------------------------------------------------------------
        // LOCATION BASED
        //
        // The place the task was created at, captured from the device GPS.
        // A Location Based task has no date and no time - the place is its
        // trigger. All four are optional: a task with no coordinates simply
        // never raises a place reminder.
        // ------------------------------------------------------------
        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public int? GeofenceRadiusMeters { get; set; }

        public bool? GeofenceEnabled { get; set; }
    }


    // ================================================================
    // CREATE GROUP REQUEST
    // ================================================================
    public class CreateGroupRequest
    {
        public string Name { get; set; }

        public List<int> MemberUserIds { get; set; } = new();

        public List<ExternalMemberDto> ExternalMembers { get; set; } = new();
    }


    // ================================================================
    // EXTERNAL MEMBER
    // ================================================================
    public class ExternalMemberDto
    {
        public string Name { get; set; }

        public string Phone { get; set; }
    }


    // ================================================================
    // UPDATE TASK REQUEST
    //
    // Supports:
    // 1. Time-based tasks
    // 2. Non-time-based tasks
    // ================================================================
    public class UpdateTaskRequest
    {
        public string Title { get; set; }

        public string Description { get; set; }

        public DateOnly? DueDate { get; set; }

        public TimeOnly? DueTime { get; set; }

        public bool IsTimeBased { get; set; }

        // Place fields for a Location Based task. Applied only when the
        // client sends them, so a partial edit cannot wipe a saved place.
        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public int? GeofenceRadiusMeters { get; set; }

        public bool? GeofenceEnabled { get; set; }
    }


    // ================================================================
    // FORWARD TASK REQUEST
    // ================================================================
    public class ForwardTaskRequest
    {
        public List<int> ToUserIds { get; set; } = new();
    }
}












//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using TODOLISTAPI.Models;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.AspNetCore.Authorization;
//using System.Security.Claims;

//namespace TODOLISTAPI.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    [Authorize]
//    public class ManagmentController : ControllerBase
//    {
//        private readonly TodoSmartAlertsContext _context;

//        public ManagmentController(TodoSmartAlertsContext context)
//        {
//            _context = context;
//        }

//        [Authorize]
//        [HttpPost("{groupId}/members")]
//        public async Task<IActionResult> AddMember( int groupId, [FromBody] AddGroupMemberDto model)
//        {
//            try
//            {
//                // Get logged-in user from JWT
//                var userIdClaim = User.FindFirst("UserId")?.Value;

//                if (string.IsNullOrEmpty(userIdClaim))
//                {
//                    return Unauthorized(new
//                    {
//                        success = false,
//                        message = "Invalid or expired token."
//                    });
//                }

//                int loggedInUserId = int.Parse(userIdClaim);

//                // Validate group belongs to logged-in user
//                var group = await _context.GroupsUsers.FirstOrDefaultAsync(g =>
//                    g.Id == groupId &&
//                    g.Id == loggedInUserId);   // <-- Use UserId here

//                if (group == null)
//                {
//                    return NotFound(new
//                    {
//                        success = false,
//                        message = "Group not found or access denied."
//                    });
//                }

//                // Validate request
//                if (string.IsNullOrWhiteSpace(model.Name))
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "Member name is required."
//                    });
//                }

//                if (string.IsNullOrWhiteSpace(model.Phone))
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "Phone number is required."
//                    });
//                }

//                // Prevent duplicate phone in same group
//                bool exists = await _context.GroupMembers.AnyAsync(x =>
//                    x.GroupId == groupId &&
//                    x.Phone == model.Phone);

//                if (exists)
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "This phone number already exists in the selected group."
//                    });
//                }

//                // Create member
//                GroupMember member = new GroupMember
//                {
//                    GroupId = groupId,
//                    UserId = model.UserId,
//                    Name = model.Name.Trim(),
//                    Phone = model.Phone.Trim(),
//                    Role = "Member",
//                    AddedAt = DateTime.Now
//                };

//                _context.GroupMembers.Add(member);

//                await _context.SaveChangesAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Member added successfully.",
//                    data = new
//                    {
//                        id = member.Id,
//                        groupId = member.GroupId,
//                        userId = member.UserId,
//                        name = member.Name,
//                        phone = member.Phone,
//                        role = member.Role,
//                        isRegistered = member.UserId != null
//                    }
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

//        //--------------------------------------------------------
//        // POST : /tasks
//        //--------------------------------------------------------
//        [HttpPost("task")]
//        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto model)
//        {
//            try
//            {
//                // Validate request
//                if (model == null)
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "Request body is required."
//                    });
//                }

//                if (string.IsNullOrWhiteSpace(model.Title))
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "Title is required."
//                    });
//                }

//                if (string.IsNullOrWhiteSpace(model.Description))
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = "Description is required."
//                    });
//                }

//                // Validate Group if supplied
//                if (model.GroupId != null)
//                {
//                    bool groupExists = await _context.GroupsUsers
//                        .AnyAsync(g => g.Id == model.GroupId);

//                    if (!groupExists)
//                    {
//                        return BadRequest(new
//                        {
//                            success = false,
//                            message = "Selected group does not exist."
//                        });
//                    }
//                }

//                // Get logged-in user ID from JWT
//                var userIdClaim = User.FindFirst("UserId")?.Value;

//                if (string.IsNullOrWhiteSpace(userIdClaim))
//                {
//                    // Also try NameIdentifier in case your JWT uses that claim
//                    userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//                }

//                if (!int.TryParse(userIdClaim, out int loggedInUserId))
//                {
//                    return Unauthorized(new
//                    {
//                        success = false,
//                        message = "Invalid or expired token."
//                    });
//                }

//                // Create task
//                var task = new Models.Task
//                {
//                    Title = model.Title.Trim(),
//                    Description = model.Description.Trim(),
//                    IsTimeBased = model.IsTimeBased,
//                    DueDate = model.DueDate,
//                    DueTime = model.DueTime,
//                    GroupId = model.GroupId,
//                    CreatedBy = loggedInUserId,
//                    AssignedTo = null,
//                    Status = "Pending",
//                    CreatedAt = DateTime.Now,
//                    UpdatedAt = DateTime.Now
//                };

//                _context.Tasks.Add(task);

//                await _context.SaveChangesAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Task has been created successfully.",
//                    data = new
//                    {
//                        id = task.Id,
//                        title = task.Title,
//                        description = task.Description,
//                        dueDate = task.DueDate,
//                        dueTime = task.DueTime,
//                        isTimeBased = task.IsTimeBased,
//                        groupId = task.GroupId,
//                        status = task.Status,
//                        createdBy = task.CreatedBy,
//                        createdAt = task.CreatedAt
//                    }
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

//        [HttpPost("group")]
//        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
//        {
//            if (request == null)
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Request body is required."
//                });
//            }

//            if (string.IsNullOrWhiteSpace(request.Name))
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Group name is required."
//                });
//            }

//            // Get logged-in user ID from JWT
//            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

//            if (string.IsNullOrWhiteSpace(userIdClaim))
//            {
//                // Also try UserId claim
//                userIdClaim = User.FindFirst("UserId")?.Value;
//            }

//            if (!int.TryParse(userIdClaim, out int currentUserId))
//            {
//                return Unauthorized(new
//                {
//                    success = false,
//                    message = "Invalid token."
//                });
//            }

//            using var transaction = await _context.Database.BeginTransactionAsync();

//            try
//            {
//                //------------------------------------------------------
//                // Create Group
//                //------------------------------------------------------
//                var group = new GroupsUser
//                {
//                    Name = request.Name.Trim(),
//                    CreatedBy = currentUserId,
//                    CreatedAt = DateTime.Now
//                };

//                _context.GroupsUsers.Add(group);

//                await _context.SaveChangesAsync();

//                //------------------------------------------------------
//                // Add creator as Admin
//                //------------------------------------------------------
//                var adminMember = new GroupMember
//                {
//                    GroupId = group.Id,
//                    UserId = currentUserId,
//                    Role = "Admin",
//                    AddedAt = DateTime.Now
//                };

//                _context.GroupMembers.Add(adminMember);

//                //------------------------------------------------------
//                // Add Registered Users
//                //------------------------------------------------------
//                if (request.MemberUserIds != null &&
//                    request.MemberUserIds.Any())
//                {
//                    foreach (var memberId in request.MemberUserIds.Distinct())
//                    {
//                        // Don't add creator twice
//                        if (memberId == currentUserId)
//                            continue;

//                        // Check whether user exists
//                        bool exists = await _context.Users
//                            .AnyAsync(x => x.Id == memberId);

//                        if (!exists)
//                            continue;

//                        _context.GroupMembers.Add(new GroupMember
//                        {
//                            GroupId = group.Id,
//                            UserId = memberId,
//                            Role = "Member",
//                            AddedAt = DateTime.Now
//                        });
//                    }
//                }

//                //------------------------------------------------------
//                // Add External Members
//                //------------------------------------------------------
//                if (request.ExternalMembers != null &&
//                    request.ExternalMembers.Any())
//                {
//                    foreach (var member in request.ExternalMembers)
//                    {
//                        if (member == null ||
//                            string.IsNullOrWhiteSpace(member.Name))
//                        {
//                            continue;
//                        }

//                        _context.GroupMembers.Add(new GroupMember
//                        {
//                            GroupId = group.Id,
//                            Name = member.Name.Trim(),
//                            Phone = member.Phone,
//                            Role = "Member",
//                            AddedAt = DateTime.Now
//                        });
//                    }
//                }

//                //------------------------------------------------------
//                // Save Members
//                //------------------------------------------------------
//                await _context.SaveChangesAsync();

//                //------------------------------------------------------
//                // Commit Transaction
//                //------------------------------------------------------
//                await transaction.CommitAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = "Group created successfully.",
//                    data = new
//                    {
//                        groupId = group.Id,
//                        groupName = group.Name
//                    }
//                });
//            }
//            catch (Exception ex)
//            {
//                await transaction.RollbackAsync();

//                return StatusCode(500, new
//                {
//                    success = false,
//                    message = ex.Message
//                });
//            }
//        }

//        [HttpPut("{id}")]
//        public async Task<IActionResult> UpdateTask(int id, UpdateTaskRequest request)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Invalid request."
//                });
//            }

//            var task = await _context.Tasks
//                                     .FirstOrDefaultAsync(x => x.Id == id);

//            if (task == null)
//            {
//                return NotFound(new
//                {
//                    success = false,
//                    message = "Task not found."
//                });
//            }

//            if (string.IsNullOrWhiteSpace(request.Title))
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Title is required."
//                });
//            }

//            //------------------------------------------------------
//            // Update Task
//            //------------------------------------------------------

//            task.Title = request.Title.Trim();

//            task.Description = request.Description?.Trim();

//            task.DueDate = request.DueDate;

//            task.IsTimeBased = request.IsTimeBased;

//            // Non-Time-Based task
//            if (!request.IsTimeBased)
//            {
//                task.DueTime = null;
//            }

//            await _context.SaveChangesAsync();

//            return Ok(new
//            {
//                success = true,
//                message = "Task updated successfully.",
//                data = new
//                {
//                    task.Id,
//                    task.Title,
//                    task.Description,
//                    task.DueDate,
//                    task.DueTime,
//                    task.IsTimeBased
//                }
//            });
//        }

//        [HttpPut("{id}")]
//        public async Task<IActionResult> UpdateTask( int id, [FromBody] UpdateTimeBasedTaskRequest request)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Invalid request."
//                });
//            }

//            if (string.IsNullOrWhiteSpace(request.Title))
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Title is required."
//                });
//            }

//            if (string.IsNullOrWhiteSpace(request.Description))
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Description is required."
//                });
//            }

//            //----------------------------------------------------
//            // Get Logged In User
//            //----------------------------------------------------

//            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

//            if (userIdClaim == null)
//            {
//                return Unauthorized(new
//                {
//                    success = false,
//                    message = "Invalid token."
//                });
//            }

//            int currentUserId = Convert.ToInt32(userIdClaim);

//            //----------------------------------------------------
//            // Find Task
//            //----------------------------------------------------

//            var task = await _context.Tasks
//                                     .FirstOrDefaultAsync(x =>
//                                         x.Id == id &&
//                                         x.CreatedBy == currentUserId);

//            if (task == null)
//            {
//                return NotFound(new
//                {
//                    success = false,
//                    message = "Task not found."
//                });
//            }

//            //----------------------------------------------------
//            // Update Task
//            //----------------------------------------------------

//            task.Title = request.Title.Trim();

//            task.Description = request.Description.Trim();

//            task.DueDate = request.DueDate;

//            task.IsTimeBased = true;

//            task.DueTime = request.DueTime;

//            //----------------------------------------------------
//            // Save
//            //----------------------------------------------------

//            await _context.SaveChangesAsync();

//            return Ok(new
//            {
//                success = true,
//                message = "Task updated successfully.",
//                data = new
//                {
//                    task.Id,
//                    task.Title,
//                    task.Description,
//                    task.DueDate,
//                    task.DueTime,
//                    task.IsTimeBased
//                }
//            });
//        }

//        [HttpPost("{taskId}/forward")]
//        public async Task<IActionResult> ForwardTask( int taskId, [FromBody] ForwardTaskRequest request)
//        {
//            if (request == null ||
//                request.ToUserIds == null ||
//                request.ToUserIds.Count == 0)
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Please select at least one user."
//                });
//            }

//            //-----------------------------------------------------
//            // Logged In User
//            //-----------------------------------------------------

//            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

//            if (claim == null)
//            {
//                return Unauthorized(new
//                {
//                    success = false,
//                    message = "Invalid token."
//                });
//            }

//            int currentUserId = Convert.ToInt32(claim);

//            //-----------------------------------------------------
//            // Find Task
//            //-----------------------------------------------------

//            var task = await _context.Tasks
//                .FirstOrDefaultAsync(x =>
//                    x.Id == taskId &&
//                    x.CreatedBy == currentUserId);

//            if (task == null)
//            {
//                return NotFound(new
//                {
//                    success = false,
//                    message = "Task not found."
//                });
//            }

//            using var transaction =
//                await _context.Database.BeginTransactionAsync();

//            try
//            {
//                int forwardedCount = 0;

//                foreach (int userId in request.ToUserIds.Distinct())
//                {
//                    //-------------------------------------------------
//                    // Skip sender
//                    //-------------------------------------------------

//                    if (userId == currentUserId)
//                        continue;

//                    //-------------------------------------------------
//                    // User Exists?
//                    //-------------------------------------------------

//                    bool userExists = await _context.Users
//                        .AnyAsync(x => x.Id == userId);

//                    if (!userExists)
//                        continue;

//                    //-------------------------------------------------
//                    // Already Forwarded?
//                    //-------------------------------------------------

//                    bool alreadyForwarded =
//                        await _context.ForwardedTasks.AnyAsync(x =>
//                            x.OriginalTaskId == taskId &&
//                            x.ForwardedTo == userId);

//                    if (alreadyForwarded)
//                        continue;

//                    //-------------------------------------------------
//                    // Insert Forwarded Task
//                    //-------------------------------------------------

//                    _context.ForwardedTasks.Add(new ForwardedTask
//                    {
//                        OriginalTaskId = taskId,
//                        ForwardedBy = currentUserId,
//                        ForwardedTo = userId,
//                        ForwardedAt = DateTime.Now
//                    });

//                    //-------------------------------------------------
//                    // Create Notification
//                    //-------------------------------------------------

//                    _context.Notifications.Add(new Notification
//                    {
//                        TaskId = taskId,
//                        UserId = userId,
//                        Message = $"A task \"{task.Title}\" has been forwarded to you.",
//                        IsRead = false,
//                        SentAt = DateTime.Now
//                    });

//                    forwardedCount++;
//                }

//                await _context.SaveChangesAsync();

//                await transaction.CommitAsync();

//                return Ok(new
//                {
//                    success = true,
//                    message = $"{forwardedCount} task(s) forwarded successfully."
//                });
//            }
//            catch (Exception ex)
//            {
//                await transaction.RollbackAsync();

//                return StatusCode(500, new
//                {
//                    success = false,
//                    message = ex.Message
//                });
//            }
//        }
//    }

//    public class AddGroupMemberDto
//    {
//        public int? UserId { get; set; }
//        public string Name { get; set; }
//        public string Phone { get; set; }
//    }

//    //For Add Task for both Time based and Non Time based task
//    public class CreateTaskDto
//    {
//        public string Title { get; set; }
//        public string Description { get; set; }
//        public DateOnly? DueDate { get; set; }
//        public TimeOnly? DueTime { get; set; }
//        public bool IsTimeBased { get; set; }
//        public int? GroupId { get; set; }
//    }

//    public class CreateGroupRequest
//    {
//        public string Name { get; set; }

//        public List<int> MemberUserIds { get; set; } = new();

//        public List<ExternalMemberDto> ExternalMembers { get; set; } = new();
//    }

//    public class ExternalMemberDto
//    {
//        public string Name { get; set; }

//        public string Phone { get; set; }
//    }

//    public class UpdateTaskRequest
//    {
//        public string Title { get; set; }

//        public string Description { get; set; }

//        public DateOnly? DueDate { get; set; }

//        public bool IsTimeBased { get; set; }
//    }

//    public class UpdateTimeBasedTaskRequest
//    {
//        public string Title { get; set; }

//        public string Description { get; set; }

//        // "2026-08-10"
//        public DateOnly? DueDate { get; set; }

//        // "14:30"
//        public TimeOnly? DueTime { get; set; }

//        public bool IsTimeBased { get; set; }
//    }

//    public class ForwardTaskRequest
//    {
//        public List<int> ToUserIds { get; set; } = new();
//    }
//}