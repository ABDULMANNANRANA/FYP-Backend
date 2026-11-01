namespace TODOLISTAPI.Models;

// One reusable row per unordered pair; Version invalidates stale accept/reject/cancel actions.
public class ContactRequest
{
    public int Id { get; set; }
    public int LowerUserId { get; set; }
    public int UpperUserId { get; set; }
    public int SenderUserId { get; set; }
    public int RecipientUserId { get; set; }
    public string? SenderAlias { get; set; }
    public string Status { get; set; } = "Pending";
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
