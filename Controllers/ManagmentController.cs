using TODOLISTAPI.Services;
using System.ComponentModel.DataAnnotations;
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

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;

            var userIdClaim = User.FindFirst("UserId")?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }

            return int.TryParse(userIdClaim, out userId);
        }

        [HttpPost("{groupId}/members")]
        public async Task<IActionResult> AddMember(int groupId, [FromBody] AddGroupMemberDto model)
        {
            try
            {
                if (!TryGetCurrentUserId(out int loggedInUserId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

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

                if (!model.UserId.HasValue && string.IsNullOrWhiteSpace(model.Phone))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Phone number is required."
                    });
                }

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

                if (group.CreatedBy != loggedInUserId)
                {
                    return Forbid();
                }

                string phone = model.Phone?.Trim() ?? "";

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

                if (model.UserId.HasValue)
                {
                    var selectedUser = await _context.Users
                        .FirstOrDefaultAsync(x => x.Id == model.UserId.Value && x.IsActive == true);

                    if (selectedUser == null)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Selected user does not exist."
                        });
                    }

                    model.Name = $"{selectedUser.FirstName} {selectedUser.LastName}".Trim();
                    phone = selectedUser.PhoneNumber ?? phone;
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
                HttpContext.RequestServices.GetRequiredService<ILogger<ManagmentController>>().LogError(ex, "Request failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "The request could not be completed. Please try again."
                });
            }
        }

        [HttpGet("task-recipients")]
        public async Task<IActionResult> GetTaskRecipients(CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();
            var directory = await TaskRecipientDirectory.LoadAsync(_context, userId, cancellationToken);
            return Ok(new { success = true, data = new { currentUserId = userId,
                contacts = directory.Contacts, groups = directory.Groups } });
        }

        [HttpPost("task")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto model)
        {
            try
            {
                if (model == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Request body is required."
                    });
                }

                if (model.Title?.Trim().Length > 300)
                    return BadRequest(new { success = false, message = "Title must be 300 characters or fewer." });
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

                if (!model.IsTimeBased.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Select either a time-based or non-time-based schedule."
                    });
                }

                if (!model.DueDate.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "DueDate is required for a new task."
                    });
                }

                if (model.IsTimeBased.Value && !model.DueTime.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "DueTime is required for a time-based task."
                    });
                }

                if (!model.IsTimeBased.Value && model.DueTime.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "DueTime must be empty for a non-time-based task."
                    });
                }

                if (model.Latitude.HasValue != model.Longitude.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Latitude and Longitude must be supplied together."
                    });
                }

                if (model.Latitude is < -90 or > 90)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Latitude must be between -90 and 90."
                    });
                }

                if (model.Longitude is < -180 or > 180)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Longitude must be between -180 and 180."
                    });
                }

                if (model.GeofenceRadiusMeters is <= 0 or > 100000)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "GeofenceRadiusMeters must be between 1 and 100000."
                    });
                }

                if (!TryGetCurrentUserId(out int loggedInUserId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid or expired token."
                    });
                }

                if (model.GroupId.HasValue && model.AssignedTo.HasValue)
                    return BadRequest(new { success = false, message = "Select either one contact or one group, not both." });
                if (model.AssignedTo is <= 0)
                    return BadRequest(new { success = false, message = "AssignedTo must be a valid user ID." });
                int? assignedTo = model.AssignedTo == loggedInUserId ? null : model.AssignedTo;
                if (assignedTo.HasValue)
                {
                    var directory = await TaskRecipientDirectory.LoadAsync(_context, loggedInUserId, HttpContext.RequestAborted);
                    if (!directory.Contacts.Any(c => c.UserId == assignedTo.Value))
                        return StatusCode(403, new { success = false, message = "Select an active registered contact from your contact list." });
                }
                var mentionIds = (model.MentionedUserIds ?? new List<int>()).Distinct().ToList();
                if (mentionIds.Count > 0)
                {
                    if (!model.GroupId.HasValue || mentionIds.Contains(loggedInUserId) || mentionIds.Any(id => id <= 0))
                        return BadRequest(new { success = false, message = "Mentions must be other registered members of the selected group." });
                    var validMentionIds = await _context.GroupMembers
                        .Where(m => m.GroupId == model.GroupId.Value && m.UserId.HasValue &&
                            mentionIds.Contains(m.UserId.Value) && m.User != null && m.User.IsActive == true)
                        .Select(m => m.UserId!.Value).Distinct().ToListAsync();
                    if (validMentionIds.Count != mentionIds.Count)
                        return BadRequest(new { success = false, message = "One or more mentioned users are not active members of the selected group." });
                }

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

                if (model.GroupId.HasValue && !await _context.GroupsUsers.AnyAsync(g =>
                    g.Id == model.GroupId.Value && (g.CreatedBy == loggedInUserId ||
                    g.GroupMembers.Any(m => m.UserId == loggedInUserId))))
                    return Forbid();

                await using var transaction = await _context.Database.BeginTransactionAsync();
                var isTimeBased = model.IsTimeBased.Value;
                TimeOnly? dueTime = isTimeBased ? model.DueTime : null;

                var task = new Models.Task
                {
                    Title = model.Title.Trim(),
                    Description = model.Description.Trim(),

                    IsTimeBased = isTimeBased,

                    DueDate = model.DueDate,
                    DueTime = dueTime,

                    GroupId = model.GroupId,

                    Latitude = model.Latitude,
                    Longitude = model.Longitude,
                    GeofenceRadiusMeters = model.GeofenceRadiusMeters ?? 200,
                    GeofenceEnabled = model.GeofenceEnabled ?? true,

                    CreatedBy = loggedInUserId,
                    AssignedTo = assignedTo,

                    Status = "Pending",

                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Tasks.Add(task);

                await _context.SaveChangesAsync();

                var creator = await _context.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == loggedInUserId);
                var creatorName = creator == null ? "A contact" : $"{creator.FirstName} {creator.LastName}".Trim();
                if (task.GroupId != null)
                {
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

                }
                else if (assignedTo.HasValue)
                {
                    _context.Notifications.Add(new Notification
                    {
                        TaskId = task.Id, UserId = assignedTo.Value, SenderId = loggedInUserId,
                        Type = "TaskAssigned", Message = $"{creatorName} assigned the task \"{task.Title}\" to you.",
                        IsRead = false, SentAt = DateTime.Now
                    });
                }
                foreach (var mentionedUserId in mentionIds)
                {
                    _context.TaskMentions.Add(new TaskMention { TaskId = task.Id, MentionedUserId = mentionedUserId,
                        MentionedBy = loggedInUserId, MentionedAt = DateTime.Now });
                    _context.Notifications.Add(new Notification { TaskId = task.Id, UserId = mentionedUserId,
                        SenderId = loggedInUserId, Type = "Mention", IsRead = false, SentAt = DateTime.Now,
                        Message = $"{creatorName} mentioned you in task \"{task.Title}\"." });
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
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
                        latitude = task.Latitude,
                        longitude = task.Longitude,
                        geofenceRadiusMeters = task.GeofenceRadiusMeters,
                        geofenceEnabled = task.GeofenceEnabled,
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
                HttpContext.RequestServices.GetRequiredService<ILogger<ManagmentController>>().LogError(ex, "Request failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "The request could not be completed. Please try again."
                });
            }
        }

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
                var group = new GroupsUser
                {
                    Name = request.Name.Trim(),
                    CreatedBy = currentUserId,
                    CreatedAt = DateTime.Now
                };

                _context.GroupsUsers.Add(group);

                await _context.SaveChangesAsync();

                var adminMember = new GroupMember
                {
                    GroupId = group.Id,
                    UserId = currentUserId,
                    Role = "Admin",
                    AddedAt = DateTime.Now
                };

                _context.GroupMembers.Add(adminMember);

                if (request.MemberUserIds != null &&
                    request.MemberUserIds.Any())
                {
                    foreach (var memberId in request.MemberUserIds.Distinct())
                    {
                        if (memberId == currentUserId)
                            continue;

                        bool userExists = await _context.Users
                            .AnyAsync(x => x.Id == memberId);

                        if (!userExists)
                            continue;

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

                await _context.SaveChangesAsync();

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
                HttpContext.RequestServices.GetRequiredService<ILogger<ManagmentController>>().LogError(ex, "Request failed");
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    success = false,
                    message = "The request could not be completed. Please try again."
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Request body is required."
                    });
                }

                if (request.Title?.Trim().Length > 300)
                    return BadRequest(new { success = false, message = "Title must be 300 characters or fewer." });
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

                if (!request.DueDate.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "DueDate is required for a task."
                    });
                }

                if (request.IsTimeBased && !request.DueTime.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "DueTime is required for a time-based task."
                    });
                }

                if (!request.IsTimeBased && request.DueTime.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "DueTime must be empty for a non-time-based task."
                    });
                }

                if (request.Latitude.HasValue != request.Longitude.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Latitude and Longitude must be supplied together."
                    });
                }

                if (request.Latitude is < -90 or > 90)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Latitude must be between -90 and 90."
                    });
                }

                if (request.Longitude is < -180 or > 180)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Longitude must be between -180 and 180."
                    });
                }

                if (request.GeofenceRadiusMeters is <= 0 or > 100000)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "GeofenceRadiusMeters must be between 1 and 100000."
                    });
                }

                if (!TryGetCurrentUserId(out int currentUserId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid token."
                    });
                }

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

                task.Title = request.Title.Trim();

                task.Description = request.Description.Trim();

                task.DueDate = request.DueDate;

                task.IsTimeBased = request.IsTimeBased;

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

                if (request.IsTimeBased)
                {
                    task.DueTime = request.DueTime;
                }
                else
                {
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
                HttpContext.RequestServices.GetRequiredService<ILogger<ManagmentController>>().LogError(ex, "Request failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "The request could not be completed. Please try again."
                });
            }
        }

        [HttpPost("{taskId}/forward")]
        public async Task<IActionResult> ForwardTask(int taskId, [FromBody] ForwardTaskRequest request)
        {
            try
            {
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

                if (!TryGetCurrentUserId(out int currentUserId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid token."
                    });
                }

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

                if (string.Equals(task.Status, "Done", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(task.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Completed tasks cannot be forwarded."
                    });
                }

                using var transaction =
                    await _context.Database.BeginTransactionAsync();

                try
                {
                    int forwardedCount = 0;

                    foreach (int userId in request.ToUserIds.Distinct())
                    {
                        if (userId == currentUserId)
                            continue;

                        bool userExists = await _context.Users
                            .AnyAsync(x => x.Id == userId);

                        if (!userExists)
                            continue;

                        bool alreadyForwarded =
                            await _context.ForwardedTasks.AnyAsync(x =>
                                x.OriginalTaskId == taskId &&
                                x.ForwardedTo == userId);

                        if (alreadyForwarded)
                            continue;

                        _context.ForwardedTasks.Add(
                            new ForwardedTask
                            {
                                OriginalTaskId = taskId,
                                ForwardedBy = currentUserId,
                                ForwardedTo = userId,
                                ForwardedAt = DateTime.Now
                            });

                        _context.Notifications.Add(
                            new Notification
                            {
                                TaskId = taskId,
                                UserId = userId,
                                SenderId = currentUserId,
                                Type = "TaskForwarded",
                                Message =
                                    $"A task \"{task.Title}\" has been forwarded to you.",
                                IsRead = false,
                                SentAt = DateTime.Now
                            });

                        forwardedCount++;
                    }

                    await _context.SaveChangesAsync();

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
                HttpContext.RequestServices.GetRequiredService<ILogger<ManagmentController>>().LogError(ex, "Request failed");
                    await transaction.RollbackAsync();

                    return StatusCode(500, new
                    {
                        success = false,
                        message = "The request could not be completed. Please try again."
                    });
                }
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<ManagmentController>>().LogError(ex, "Request failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "The request could not be completed. Please try again."
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
                            ? ((m.User!.FirstName ?? "") + " " + (m.User!.LastName ?? "")).Trim()
                            : (m.Name ?? "External Member"),

                        firstName = m.UserId != null
                            ? m.User!.FirstName
                            : m.Name,

                        lastName = m.UserId != null
                            ? m.User!.LastName
                            : "",

                        email = m.UserId != null
                            ? m.User!.Email
                            : null,

                        phone = m.UserId != null
                            ? m.User!.PhoneNumber
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
                HttpContext.RequestServices.GetRequiredService<ILogger<ManagmentController>>().LogError(ex, "Request failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "The request could not be completed. Please try again."
                });
            }
        }

    }

    public class AddGroupMemberDto
    {
        public int? UserId { get; set; }

        [StringLength(150)]
        public string Name { get; set; } = "";

        [StringLength(20)]
        public string Phone { get; set; } = "";
    }

    public class CreateTaskDto
    {
        [StringLength(300)]
        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public DateOnly? DueDate { get; set; }

        public TimeOnly? DueTime { get; set; }

        public bool? IsTimeBased { get; set; }

        public int? GroupId { get; set; }

        // Null for Myself/group tasks; a registered contact's USER ID for an individual task.
        public int? AssignedTo { get; set; }

        [MaxLength(100)]
        public List<int>? MentionedUserIds { get; set; } = new();

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public int? GeofenceRadiusMeters { get; set; }

        public bool? GeofenceEnabled { get; set; }
    }

    public class CreateGroupRequest
    {
        [StringLength(150)]
        public string Name { get; set; } = "";

        [MaxLength(100)]
        public List<int> MemberUserIds { get; set; } = new();

        public List<ExternalMemberDto> ExternalMembers { get; set; } = new();
    }

    public class ExternalMemberDto
    {
        [StringLength(150)]
        public string Name { get; set; } = "";

        [StringLength(20)]
        public string Phone { get; set; } = "";
    }

    public class UpdateTaskRequest
    {
        [StringLength(300)]
        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public DateOnly? DueDate { get; set; }

        public TimeOnly? DueTime { get; set; }

        public bool IsTimeBased { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public int? GeofenceRadiusMeters { get; set; }

        public bool? GeofenceEnabled { get; set; }
    }

    public class ForwardTaskRequest
    {
        [MaxLength(100)]
        public List<int> ToUserIds { get; set; } = new();
    }
}

