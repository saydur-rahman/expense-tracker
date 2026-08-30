using System.ComponentModel.DataAnnotations;
using ExpenseTracker019.Api.Models;

namespace ExpenseTracker019.Api.Dtos.Feedback;

public class FeedbackMessageDto
{
    public Guid Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public bool IsFromAdmin { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class FeedbackDto
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public FeedbackStatus Status { get; set; }

    /// <summary>Admin-facing; empty on a user's own list, where it would only repeat themselves.</summary>
    public string SubmittedByName { get; set; } = string.Empty;
    public string SubmittedByEmail { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public int MessageCount { get; set; }

    /// <summary>False once resolved — the UI hides the reply box rather than inviting a rejection.</summary>
    public bool CanReply { get; set; }

    public List<FeedbackMessageDto> Messages { get; set; } = new();
}

public class SubmitFeedbackRequest
{
    [Required(ErrorMessage = "Give your feedback a subject.")]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tell us what you'd like to say.")]
    [MaxLength(4000)]
    public string Message { get; set; } = string.Empty;
}

public class ReplyRequest
{
    [Required(ErrorMessage = "Write a reply first.")]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;
}

public class UpdateFeedbackStatusRequest
{
    [Required]
    public FeedbackStatus Status { get; set; }
}

public class FeedbackListDto
{
    public List<FeedbackDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
}
