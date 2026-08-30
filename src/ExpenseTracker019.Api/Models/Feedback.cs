namespace ExpenseTracker019.Api.Models;

public enum FeedbackStatus
{
    /// <summary>Submitted, nobody has picked it up.</summary>
    Open = 0,

    /// <summary>An admin is working on it. Both sides can still reply.</summary>
    InProgress = 1,

    /// <summary>Closed by an admin. No further replies are accepted from anyone.</summary>
    Resolved = 2,
}

/// <summary>
/// One conversation between a user and the admins, carrying its own thread of messages.
/// </summary>
public class Feedback
{
    public Guid Id { get; set; }

    /// <summary>The submitter, from the token's <c>sub</c> claim.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Who sent it, captured when it was submitted.
    /// </summary>
    /// <remarks>
    /// A snapshot, not a user record — this service still owns no user table and cannot
    /// join to one across the service boundary. Admins need to see who they are replying
    /// to without a round trip to Auth019 for every row, and a later rename shouldn't
    /// rewrite the history of a conversation.
    /// </remarks>
    public string SubmittedByName { get; set; } = string.Empty;
    public string SubmittedByEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Moves with the newest message or status change, so admins can sort by activity.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }

    public ICollection<FeedbackMessage> Messages { get; set; } = new List<FeedbackMessage>();
}

/// <summary>One message in a feedback thread, from either the user or an admin.</summary>
public class FeedbackMessage
{
    public Guid Id { get; set; }

    public Guid FeedbackId { get; set; }
    public Feedback Feedback { get; set; } = null!;

    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>Drives which side of the thread this appears on.</summary>
    public bool IsFromAdmin { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
