using ExpenseTracker019.Api.Dtos.Feedback;
using ExpenseTracker019.Api.Models;

namespace ExpenseTracker019.Api.Services;

public interface IFeedbackService
{
    // --- the user's own conversations ---
    Task<FeedbackDto> SubmitAsync(Guid userId, string authorName, string authorEmail, SubmitFeedbackRequest request);
    Task<IReadOnlyList<FeedbackDto>> ListMineAsync(Guid userId);
    Task<FeedbackDto> GetMineAsync(Guid userId, Guid feedbackId);
    Task<FeedbackDto> ReplyAsMineAsync(Guid userId, string authorName, Guid feedbackId, string body);

    // --- admin ---
    Task<FeedbackListDto> ListAllAsync(FeedbackStatus? status);
    Task<FeedbackDto> GetAnyAsync(Guid feedbackId);
    Task<FeedbackDto> ReplyAsAdminAsync(Guid adminUserId, string adminName, Guid feedbackId, string body);
    Task<FeedbackDto> SetStatusAsync(Guid feedbackId, FeedbackStatus status);
}
