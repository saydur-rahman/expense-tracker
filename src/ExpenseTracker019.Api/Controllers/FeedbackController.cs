using ExpenseTracker019.Api.Dtos.Feedback;
using ExpenseTracker019.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker019.Api.Controllers;

/// <summary>
/// A user's own feedback conversations. Everything is scoped to the <c>sub</c> claim,
/// so an id from someone else's thread reads as "not found" rather than leaking it.
/// </summary>
[ApiController]
[Route("api/feedback")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedback;
    private readonly ICurrentUser _currentUser;

    public FeedbackController(IFeedbackService feedback, ICurrentUser currentUser)
    {
        _feedback = feedback;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FeedbackDto>>> ListMine()
        => Ok(await _feedback.ListMineAsync(_currentUser.Id));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FeedbackDto>> GetMine(Guid id)
        => Ok(await _feedback.GetMineAsync(_currentUser.Id, id));

    [HttpPost]
    public async Task<ActionResult<FeedbackDto>> Submit(SubmitFeedbackRequest request)
        => Ok(await _feedback.SubmitAsync(
            _currentUser.Id, _currentUser.DisplayName, _currentUser.Email, request));

    [HttpPost("{id:guid}/replies")]
    public async Task<ActionResult<FeedbackDto>> Reply(Guid id, ReplyRequest request)
        => Ok(await _feedback.ReplyAsMineAsync(
            _currentUser.Id, _currentUser.DisplayName, id, request.Body));
}
