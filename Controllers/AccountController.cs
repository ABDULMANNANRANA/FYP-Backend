using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TODOLISTAPI.Models;

namespace TODOLISTAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountController(TodoSmartAlertsContext context) : ControllerBase
{
    private int? CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("UserId"), out var id) ? id : null;

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var user = await context.Users.AsNoTracking()
            .Where(u => u.Id == CurrentUserId && u.IsActive == true)
            .Select(u => new { userId = u.Id, fullName = (u.FirstName + " " + u.LastName).Trim(),
                email = u.Email, phoneNumber = u.PhoneNumber }).SingleOrDefaultAsync();
        return user == null ? Unauthorized(new { success = false, message = "Account unavailable. Please sign in again." })
            : Ok(new { success = true, data = user });
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await context.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == CurrentUserId && u.IsActive == true);
        if (user == null) return Unauthorized(new { success = false, message = "Account unavailable. Please sign in again." });
        var hasher = new PasswordHasher<User>();
        var verified = PasswordVerificationResult.Failed;
        try { verified = hasher.VerifyHashedPassword(user, user.Password, request.CurrentPassword); }
        catch (FormatException) { /* Never accept plaintext password values. */ }
        if (verified == PasswordVerificationResult.Failed)
            return BadRequest(new { success = false, message = "Current password is incorrect." });
        if (request.NewPassword == request.CurrentPassword)
            return BadRequest(new { success = false, message = "Choose a different new password." });
        var oldHash = user.Password;
        var newHash = hasher.HashPassword(user, request.NewPassword);
        // Only the authenticated user's password changes. A concurrent change must not be overwritten.
        var changed = await context.Users.Where(u => u.Id == user.Id && u.Password == oldHash && u.IsActive == true)
            .ExecuteUpdateAsync(update => update.SetProperty(u => u.Password, newHash));
        return changed == 1 ? Ok(new { success = true, message = "Password changed successfully." })
            : Conflict(new { success = false, message = "Account changed. Please sign in again before retrying." });
    }
}

public class ChangePasswordRequest
{
    [Required, StringLength(128)] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}
