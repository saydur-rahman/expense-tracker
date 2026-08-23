using ExpenseTracker.Api.Dtos.Categories;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly ICurrentUser _currentUser;

    public CategoriesController(ICategoryService categoryService, ICurrentUser currentUser)
    {
        _categoryService = categoryService;
        _currentUser = currentUser;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> List([FromQuery] bool includeArchived = false)
        => Ok(await _categoryService.ListAsync(_currentUser.Id, includeArchived));

    [HttpPost("categories")]
    public async Task<ActionResult<CategoryDto>> Create(SaveCategoryRequest request)
        => Ok(await _categoryService.CreateAsync(_currentUser.Id, request.Name));

    [HttpPut("categories/{id:guid}")]
    public async Task<ActionResult<CategoryDto>> Rename(Guid id, SaveCategoryRequest request)
        => Ok(await _categoryService.RenameAsync(_currentUser.Id, id, request.Name));

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id)
    {
        await _categoryService.ArchiveAsync(_currentUser.Id, id);
        return NoContent();
    }

    [HttpPost("categories/{categoryId:guid}/heads")]
    public async Task<ActionResult<HeadDto>> CreateHead(Guid categoryId, SaveHeadRequest request)
        => Ok(await _categoryService.CreateHeadAsync(_currentUser.Id, categoryId, request.Name));

    [HttpPut("heads/{id:guid}")]
    public async Task<ActionResult<HeadDto>> RenameHead(Guid id, SaveHeadRequest request)
        => Ok(await _categoryService.RenameHeadAsync(_currentUser.Id, id, request.Name));

    [HttpDelete("heads/{id:guid}")]
    public async Task<IActionResult> ArchiveHead(Guid id)
    {
        await _categoryService.ArchiveHeadAsync(_currentUser.Id, id);
        return NoContent();
    }
}
