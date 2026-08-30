using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Dtos.Feedback;
using ExpenseTracker019.Api.Exceptions;
using ExpenseTracker019.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker019.Api.Services;

public class FeedbackService : IFeedbackService
{
    private readonly AppDbContext _db;

    public FeedbackService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FeedbackDto> SubmitAsync(
        Guid userId, string authorName, string authorEmail, SubmitFeedbackRequest request)
    {
        var subject = Require(request.Subject, "Give your feedback a subject.");
        var body = Require(request.Message, "Tell us what you'd like to say.");

        var feedback = new Feedback
        {
            UserId = userId,
            SubmittedByName = authorName,
            SubmittedByEmail = authorEmail,
            Subject = subject,
            Status = FeedbackStatus.Open,
        };

        // The opening message is just the first message in the thread, so the
        // conversation has one shape throughout rather than a special first entry.
        feedback.Messages.Add(new FeedbackMessage
        {
            AuthorUserId = userId,
            AuthorName = authorName,
            IsFromAdmin = false,
            Body = body,
        });

        _db.Feedbacks.Add(feedback);
        await _db.SaveChangesAsync();

        return ToDto(feedback, includeSubmitter: false);
    }

    public async Task<IReadOnlyList<FeedbackDto>> ListMineAsync(Guid userId)
    {
        var items = await _db.Feedbacks
            .Include(f => f.Messages)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.UpdatedAtUtc)
            .ToListAsync();

        return items.Select(f => ToDto(f, includeSubmitter: false, includeMessages: false)).ToList();
    }

    public async Task<FeedbackDto> GetMineAsync(Guid userId, Guid feedbackId)
    {
        var feedback = await LoadAsync(feedbackId);

        // Tenant isolation: the id alone must never be enough to read someone else's thread.
        if (feedback.UserId != userId)
        {
            throw new NotFoundAppException("Feedback not found.");
        }

        return ToDto(feedback, includeSubmitter: false);
    }

    public async Task<FeedbackDto> ReplyAsMineAsync(Guid userId, string authorName, Guid feedbackId, string body)
    {
        var feedback = await LoadAsync(feedbackId);
        if (feedback.UserId != userId)
        {
            throw new NotFoundAppException("Feedback not found.");
        }

        return await AppendAsync(feedback, userId, authorName, isFromAdmin: false, body);
    }

    public async Task<FeedbackListDto> ListAllAsync(FeedbackStatus? status)
    {
        var query = _db.Feedbacks.Include(f => f.Messages).AsQueryable();
        if (status is not null)
        {
            query = query.Where(f => f.Status == status);
        }

        var items = await query.OrderByDescending(f => f.UpdatedAtUtc).ToListAsync();

        return new FeedbackListDto
        {
            Items = items.Select(f => ToDto(f, includeSubmitter: true, includeMessages: false)).ToList(),
            TotalCount = await _db.Feedbacks.CountAsync(),
            OpenCount = await _db.Feedbacks.CountAsync(f => f.Status == FeedbackStatus.Open),
            InProgressCount = await _db.Feedbacks.CountAsync(f => f.Status == FeedbackStatus.InProgress),
        };
    }

    public async Task<FeedbackDto> GetAnyAsync(Guid feedbackId)
        => ToDto(await LoadAsync(feedbackId), includeSubmitter: true);

    public async Task<FeedbackDto> ReplyAsAdminAsync(Guid adminUserId, string adminName, Guid feedbackId, string body)
    {
        var feedback = await LoadAsync(feedbackId);

        // Replying is the act of picking it up, so an untouched thread moves itself along.
        if (feedback.Status == FeedbackStatus.Open)
        {
            feedback.Status = FeedbackStatus.InProgress;
        }

        return await AppendAsync(feedback, adminUserId, adminName, isFromAdmin: true, body);
    }

    public async Task<FeedbackDto> SetStatusAsync(Guid feedbackId, FeedbackStatus status)
    {
        var feedback = await LoadAsync(feedbackId);

        feedback.Status = status;
        feedback.ResolvedAtUtc = status == FeedbackStatus.Resolved ? DateTime.UtcNow : null;
        feedback.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToDto(feedback, includeSubmitter: true);
    }

    /// <summary>
    /// Adds a message, refusing once the thread is resolved. Enforced here rather than in
    /// the controllers so neither side — user or admin — can post to a closed conversation.
    /// </summary>
    private async Task<FeedbackDto> AppendAsync(
        Feedback feedback, Guid authorId, string authorName, bool isFromAdmin, string body)
    {
        if (feedback.Status == FeedbackStatus.Resolved)
        {
            throw new ValidationAppException(
                "This feedback has been closed, so it can't take any more replies. " +
                "Submit new feedback if there's more to say.");
        }

        feedback.Messages.Add(new FeedbackMessage
        {
            FeedbackId = feedback.Id,
            AuthorUserId = authorId,
            AuthorName = authorName,
            IsFromAdmin = isFromAdmin,
            Body = Require(body, "Write a reply first."),
        });

        feedback.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ToDto(feedback, includeSubmitter: isFromAdmin);
    }

    private async Task<Feedback> LoadAsync(Guid feedbackId)
        => await _db.Feedbacks
               .Include(f => f.Messages)
               .FirstOrDefaultAsync(f => f.Id == feedbackId)
           ?? throw new NotFoundAppException("Feedback not found.");

    private static string Require(string value, string message)
    {
        value = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationAppException(message);
        }
        return value;
    }

    private static FeedbackDto ToDto(Feedback f, bool includeSubmitter, bool includeMessages = true) => new()
    {
        Id = f.Id,
        Subject = f.Subject,
        Status = f.Status,
        SubmittedByName = includeSubmitter ? f.SubmittedByName : string.Empty,
        SubmittedByEmail = includeSubmitter ? f.SubmittedByEmail : string.Empty,
        CreatedAtUtc = f.CreatedAtUtc,
        UpdatedAtUtc = f.UpdatedAtUtc,
        ResolvedAtUtc = f.ResolvedAtUtc,
        MessageCount = f.Messages.Count,
        CanReply = f.Status != FeedbackStatus.Resolved,
        Messages = includeMessages
            ? f.Messages
                .OrderBy(m => m.CreatedAtUtc)
                .Select(m => new FeedbackMessageDto
                {
                    Id = m.Id,
                    AuthorName = m.AuthorName,
                    IsFromAdmin = m.IsFromAdmin,
                    Body = m.Body,
                    CreatedAtUtc = m.CreatedAtUtc,
                })
                .ToList()
            : new List<FeedbackMessageDto>(),
    };
}
