using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TODOLISTAPI.Models;
using TODOLISTAPI.Services;
using Task = System.Threading.Tasks.Task;

namespace TODOLISTAPI.Controllers;

[ApiController, Authorize, Route("api/User")]
public class ContactsController(TodoSmartAlertsContext db) : ControllerBase
{
    private int CurrentId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId"), out var id) ? id : 0;
    private IActionResult Stale() => Conflict(new { success = false, message = "This request has changed. Refresh Pending Requests and try again." });
    private static string FullName(User user) { var name = $"{user.FirstName} {user.LastName}".Trim(); if (name.Length == 0) name = "Contact"; return name[..Math.Min(name.Length, 150)]; }
    private static string Canonical(string? phone) => TaskRecipientDirectory.NormalizePhone(phone);
    private async Task<List<User>> Resolve(string phone)
    {
        var canonical = Canonical(phone);
        var variants = TaskRecipientDirectory.PhoneVariants(canonical).ToList();
        var users = await db.Users.AsNoTracking().Where(u => u.IsActive == true && u.PhoneNumber != null &&
            variants.Contains(u.PhoneNumber.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "")
                .Replace(".", "").Replace("\u00a0", "").Replace("\t", "").Replace("\r", "").Replace("\n", ""))).ToListAsync();
        return users.Where(u => Canonical(u.PhoneNumber) == canonical).ToList();
    }
    private async Task<bool> HasContact(int owner, User target)
    {
        var rows = await db.PersonalContacts.AsNoTracking().Where(c => c.UserId == owner).ToListAsync();
        return rows.Any(c => c.ContactUserId == target.Id || (c.ContactUserId == null && Canonical(c.Phone) == Canonical(target.PhoneNumber)));
    }
    [HttpGet("contacts")]
    public async Task<IActionResult> Contacts()
    {
        if (CurrentId <= 0) return Unauthorized();
        var data = await db.PersonalContacts.AsNoTracking().Where(c => c.UserId == CurrentId)
            .OrderBy(c => c.Name).Select(c => new { c.Id, c.Name, phone = c.ContactUserId.HasValue ? (db.Users.Where(u => u.Id == c.ContactUserId).Select(u => u.PhoneNumber).FirstOrDefault() ?? "") : c.Phone, c.AddedAt, c.ContactUserId }).ToListAsync();
        return Ok(new { success = true, data });
    }
    // Keep the old POST URL safe too: an old mobile client cannot bypass consent.
    [HttpPost("contacts"), HttpPost("contact-requests")]
    public async Task<IActionResult> Send(SendContactRequestDto input)
    {
        if (CurrentId <= 0) return Unauthorized();
        if (!await db.Users.AnyAsync(u => u.Id == CurrentId && u.IsActive == true)) return Forbid();
        if (Canonical(input.Phone).Length is < 7 or > 15)
            return BadRequest(new { success = false, message = "Enter a valid registered phone number." });
        var matches = await Resolve(input.Phone);
        if (matches.Count != 1) return BadRequest(new { success = false, message = "This phone must match exactly one active registered app account." });
        var target = matches[0];
        if (target.Id == CurrentId) return BadRequest(new { success = false, message = "You cannot send a contact request to yourself." });
        if (await HasContact(CurrentId, target)) return Conflict(new { success = false, message = "This person is already in your contacts." });
        int lower = Math.Min(CurrentId, target.Id), upper = Math.Max(CurrentId, target.Id);
        var request = await db.ContactRequests.SingleOrDefaultAsync(r => r.LowerUserId == lower && r.UpperUserId == upper);
        if (request?.Status == "Pending") return Conflict(new { success = false, message = request.SenderUserId == CurrentId
            ? "Your request is already pending." : "This person already sent you a request. Accept it in Pending Requests." });
        if (request?.Status == "Accepted") return Conflict(new { success = false, message = "This contact is already accepted. Refresh your contacts." });
        if (request == null) { request = new ContactRequest { LowerUserId = lower, UpperUserId = upper }; db.ContactRequests.Add(request); }
        request.SenderUserId = CurrentId; request.RecipientUserId = target.Id;
        request.SenderAlias = string.IsNullOrWhiteSpace(input.Alias ?? input.Name) ? null : (input.Alias ?? input.Name)!.Trim();
        request.Status = "Pending"; request.Version = Guid.NewGuid();
        request.CreatedAt = request.UpdatedAt = DateTime.UtcNow;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Stale(); }
        catch (DbUpdateException) { return Conflict(new { success = false, message = "The contact request changed or was already sent. Refresh and try again." }); }
        return Ok(new { success = true, message = "Contact request sent. When accepted, you will both appear in each other's contacts.",
            data = new { request.Id, request.Version, request.Status } });
    }
    [HttpGet("contact-requests")]
    public async Task<IActionResult> Pending()
    {
        if (CurrentId <= 0) return Unauthorized();
        var requests = await db.ContactRequests.AsNoTracking().Where(r => r.Status == "Pending" &&
            (r.SenderUserId == CurrentId || r.RecipientUserId == CurrentId)).OrderByDescending(r => r.CreatedAt).ToListAsync();
        var ids = requests.SelectMany(r => new[] { r.SenderUserId, r.RecipientUserId }).Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        object Display(ContactRequest r, bool incoming) {
            var person = people[incoming ? r.SenderUserId : r.RecipientUserId];
            return new { r.Id, r.Version, r.Status, r.CreatedAt, userId = person.Id,
                name = incoming || string.IsNullOrWhiteSpace(r.SenderAlias) ? FullName(person) : r.SenderAlias,
                phone = person.PhoneNumber, direction = incoming ? "received" : "sent" };
        }
        return Ok(new { success = true, data = new {
            received = requests.Where(r => r.RecipientUserId == CurrentId).Select(r => Display(r, true)),
            sent = requests.Where(r => r.SenderUserId == CurrentId).Select(r => Display(r, false)) } });
    }
    [HttpPost("contact-requests/{id:int}/accept")]
    public Task<IActionResult> Accept(int id, ContactRequestActionDto input) => Change(id, input, "Accepted");
    [HttpPost("contact-requests/{id:int}/reject")]
    public Task<IActionResult> Reject(int id, ContactRequestActionDto input) => Change(id, input, "Rejected");
    [HttpPost("contact-requests/{id:int}/cancel")]
    public Task<IActionResult> Cancel(int id, ContactRequestActionDto input) => Change(id, input, "Cancelled");
    private async Task<IActionResult> Change(int id, ContactRequestActionDto input, string next)
    {
        if (CurrentId <= 0) return Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var request = await db.ContactRequests.SingleOrDefaultAsync(r => r.Id == id);
        if (request == null || (request.SenderUserId != CurrentId && request.RecipientUserId != CurrentId)) return NotFound();
        if ((next == "Cancelled" ? request.SenderUserId : request.RecipientUserId) != CurrentId) return Forbid();
        if (input.Version != request.Version || request.Status != "Pending") return Stale();
        if (next == "Accepted")
        {
            var users = await db.Users.Where(u => u.Id == request.SenderUserId || u.Id == request.RecipientUserId).ToListAsync();
            if (users.Count != 2 || users.Any(u => u.IsActive != true)) return BadRequest(new { success = false, message = "Both accounts must be active to accept." });
            var sender = users.Single(u => u.Id == request.SenderUserId);
            var recipient = users.Single(u => u.Id == request.RecipientUserId);
            await UpsertContact(sender.Id, recipient, request.SenderAlias);
            await UpsertContact(recipient.Id, sender, null);
        }
        request.Status = next; request.Version = Guid.NewGuid(); request.UpdatedAt = DateTime.UtcNow;
        try { await db.SaveChangesAsync(); await transaction.CommitAsync(); }
        catch (DbUpdateConcurrencyException) { return Stale(); }
        catch (DbUpdateException) { return Conflict(new { success = false, message = "The request could not be updated. Refresh and try again." }); }
        return Ok(new { success = true, message = next == "Accepted" ? "Contact request accepted. You are now in each other's contacts." : $"Contact request {next.ToLowerInvariant()}.", data = new { request.Id, request.Status } });
    }
    private async Task UpsertContact(int owner, User target, string? alias)
    {
        var rows = await db.PersonalContacts.Where(c => c.UserId == owner).ToListAsync();
        var matches = rows.Where(c => c.ContactUserId == target.Id || (c.ContactUserId == null && Canonical(c.Phone) == Canonical(target.PhoneNumber))).OrderBy(c => c.Id).ToList();
        var row = matches.FirstOrDefault();
        if (row == null) { row = new PersonalContact { UserId = owner, AddedAt = DateTime.UtcNow, Name = FullName(target), Phone = string.IsNullOrWhiteSpace(target.PhoneNumber) ? $"user:{target.Id}" : target.PhoneNumber }; db.PersonalContacts.Add(row); }
        row.ContactUserId = target.Id;
        if (!string.IsNullOrWhiteSpace(alias)) row.Name = alias;
        // Do not overwrite a recipient's existing private alias.
        if (matches.Count > 1) db.PersonalContacts.RemoveRange(matches.Skip(1));
    }
    [HttpDelete("contacts/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (CurrentId <= 0) return Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var contact = await db.PersonalContacts.SingleOrDefaultAsync(c => c.Id == id && c.UserId == CurrentId);
        if (contact == null) return NotFound();
        if (contact.ContactUserId is int other)
        {
            var lower = Math.Min(CurrentId, other); var upper = Math.Max(CurrentId, other);
            var request = await db.ContactRequests.SingleOrDefaultAsync(r => r.LowerUserId == lower && r.UpperUserId == upper);
            if (request != null) { request.Status = "Removed"; request.Version = Guid.NewGuid(); request.UpdatedAt = DateTime.UtcNow; }
            var rows = await db.PersonalContacts.Where(c => (c.UserId == CurrentId && c.ContactUserId == other) ||
                (c.UserId == other && c.ContactUserId == CurrentId)).ToListAsync();
            db.PersonalContacts.RemoveRange(rows);
        }
        else db.PersonalContacts.Remove(contact); // Preserve pre-existing one-sided legacy semantics.
        try { await db.SaveChangesAsync(); await transaction.CommitAsync(); }
        catch (DbUpdateConcurrencyException) { return Stale(); }
        return Ok(new { success = true, message = "Contact removed. A new request is needed to connect again." });
    }
}
public class SendContactRequestDto
{
    [Required, StringLength(32)] public string Phone { get; set; } = "";
    [StringLength(150)] public string? Alias { get; set; }
    [StringLength(150)] public string? Name { get; set; } // old client compatibility: still sends a request
}
public class ContactRequestActionDto
{
    [Required] public Guid? Version { get; set; }
}
