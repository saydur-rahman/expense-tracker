using ExpenseTracker019.Api.Dtos.Feedback;
using ExpenseTracker019.Api.Models;
using ExpenseTracker019.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker019.Api.Controllers;

/// <summary>
/// Every user's feedback, for admins. Guarded by the Admin role rather than ownership —
/// the one place in this service that deliberately reads across users.
/// </summary>
/// <remarks>
/// An impersonation token carries no roles, so an impersonated session cannot reach any
/// of this, and cannot answer feedback while wearing someone else's identity.
/// </remarks>
[ApiController]
[Route("api/admin/feedback")]
[Authorize(Policy = AuthPolicies.Admin)]
public class AdminFeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedback;
    private readonly ICurrentUser _currentUser;

    public AdminFeedbackController(IFeedbackService feedback, ICurrentUser currentUser)
    {
        _feedback = feedback;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<FeedbackListDto>> List([FromQuery] FeedbackStatus? status = null)
        => Ok(await _feedback.ListAllAsync(status));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FeedbackDto>> Get(Guid id)
        => Ok(await _feedback.GetAnyAsync(id));

    [HttpPost("{id:guid}/replies")]
    public async Task<ActionResult<FeedbackDto>> Reply(Guid id, ReplyRequest request)
        => Ok(await _feedback.ReplyAsAdminAsync(
            _currentUser.Id, _currentUser.DisplayName, id, request.Body));

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<FeedbackDto>> SetStatus(Guid id, UpdateFeedbackStatusRequest request)
        => Ok(await _feedback.SetStatusAsync(id, request.Status));
}
