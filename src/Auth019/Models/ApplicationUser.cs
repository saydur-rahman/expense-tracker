using Microsoft.AspNetCore.Identity;

namespace Auth019.Models;

/// <summary>
/// The identity record. Auth019 is the sole owner of user data — the expense API
/// knows users only by the <c>sub</c> claim on the tokens issued here.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// ISO 3166-1 alpha-2 code. Nullable because accounts that never went through the
    /// registration form — external-provider sign-ups, and any account created before
    /// this was collected — have no country to record.
    /// </summary>
    public string? Country { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
}
