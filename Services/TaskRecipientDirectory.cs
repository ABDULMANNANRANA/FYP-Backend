using Microsoft.EntityFrameworkCore;
using TODOLISTAPI.Models;

namespace TODOLISTAPI.Services;

// This is deliberately shared by the dropdown endpoint and POST validation.
// A client-supplied user ID is never enough to make someone an eligible contact.
public static class TaskRecipientDirectory
{
    public sealed record Contact(int UserId, string Name, string Phone);
    public sealed record Group(int GroupId, string Name);
    public sealed record Directory(List<Contact> Contacts, List<Group> Groups);
    private sealed record RegisteredUser(int Id, string FirstName, string LastName, string? PhoneNumber);

    public static async Task<Directory> LoadAsync(
        TodoSmartAlertsContext db, int currentUserId, CancellationToken cancellationToken = default)
    {
        var groups = await db.GroupsUsers.AsNoTracking()
            .Where(g => g.CreatedBy == currentUserId || g.GroupMembers.Any(m => m.UserId == currentUserId))
            .OrderBy(g => g.Name)
            .Select(g => new Group(g.Id, g.Name))
            .ToListAsync(cancellationToken);
        // Group membership is not contact consent. Groups remain independent;
        // only accepted/legacy PersonalContact rows authorize direct assignments.
        var saved = await db.PersonalContacts.AsNoTracking()
            .Where(c => c.UserId == currentUserId).OrderBy(c => c.Id)
            .Select(c => new { c.Name, c.Phone, c.ContactUserId }).ToListAsync(cancellationToken);
        var aliases = saved.Where(c => c.ContactUserId == null && NormalizePhone(c.Phone).Length > 0)
            .GroupBy(c => NormalizePhone(c.Phone)).ToDictionary(g => g.Key, g => g.First());
        var phoneVariants = aliases.Keys.SelectMany(PhoneVariants).Distinct().ToList();

        // Filter on the server before materializing. Do not return the global list
        // of account phone numbers to the client. Formatting is canonicalized again
        // below; Pakistani +92 / 0092 / 03 forms are supported explicitly.
        var phoneMatches = new List<RegisteredUser>();
        if (phoneVariants.Count > 0)
        {
            phoneMatches = await db.Users.AsNoTracking()
                .Where(u => u.IsActive == true && u.PhoneNumber != null &&
                    phoneVariants.Contains(u.PhoneNumber.Replace(" ", "").Replace("+", "")
                        .Replace("-", "").Replace("(", "").Replace(")", "").Replace(".", "")
                        .Replace("\u00a0", "").Replace("\t", "").Replace("\r", "").Replace("\n", "")))
                .Select(u => new RegisteredUser(u.Id, u.FirstName, u.LastName, u.PhoneNumber))
                .ToListAsync(cancellationToken);
        }
        // PhoneNumber is not unique in the current schema. Do not pick an arbitrary
        // account if two active users share the same saved contact number.
        var matchingCounts = phoneMatches.GroupBy(u => NormalizePhone(u.PhoneNumber))
            .ToDictionary(g => g.Key, g => g.Count());
        var unambiguousMatches = phoneMatches.GroupBy(u => NormalizePhone(u.PhoneNumber))
            .Where(g => aliases.ContainsKey(g.Key) && g.Count() == 1)
            .Select(g => g.Single()).Where(u => u.Id != currentUserId);
        var linkedIds = saved.Where(c => c.ContactUserId.HasValue).Select(c => c.ContactUserId!.Value).Distinct().ToList();
        var linkedUsers = await db.Users.AsNoTracking().Where(u => linkedIds.Contains(u.Id) && u.Id != currentUserId && u.IsActive == true)
            .Select(u => new RegisteredUser(u.Id, u.FirstName, u.LastName, u.PhoneNumber)).ToListAsync(cancellationToken);
        var contacts = linkedUsers.Concat(unambiguousMatches).GroupBy(u => u.Id).Select(g =>
        {
            var user = g.First();
            var linked = saved.FirstOrDefault(c => c.ContactUserId == user.Id);
            var phone = NormalizePhone(user.PhoneNumber);
            var name = linked?.Name ?? (((!matchingCounts.TryGetValue(phone, out var count) || count == 1) &&
                aliases.TryGetValue(phone, out var alias) && !string.IsNullOrWhiteSpace(alias.Name))
                ? alias.Name.Trim() : $"{user.FirstName} {user.LastName}".Trim());
            return new Contact(user.Id, string.IsNullOrWhiteSpace(name) ? "Registered contact" : name,
                user.PhoneNumber?.Trim() ?? "");
        }).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.UserId).ToList();
        return new Directory(contacts, groups);
    }

    public static string NormalizePhone(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
        if (digits.Length == 12 && digits.StartsWith("923", StringComparison.Ordinal))
            return "0" + digits[2..];
        return digits;
    }
    public static IEnumerable<string> PhoneVariants(string canonical)
    {
        yield return canonical;
        yield return "00" + canonical;
        if (canonical.Length == 11 && canonical.StartsWith("03", StringComparison.Ordinal))
        {
            yield return "92" + canonical[1..];
            yield return "0092" + canonical[1..];
        }
    }
}
