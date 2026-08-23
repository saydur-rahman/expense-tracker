namespace Auth019.Dtos;

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AdminUserListDto
{
    public List<AdminUserDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
