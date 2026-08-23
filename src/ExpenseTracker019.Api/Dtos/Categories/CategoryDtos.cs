using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker019.Api.Dtos.Categories;

public class HeadDto
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public List<HeadDto> Heads { get; set; } = new();
}

public class SaveCategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class SaveHeadRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
