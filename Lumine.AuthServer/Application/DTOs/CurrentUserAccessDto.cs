namespace Lumine.AuthServer.Application.DTOs;

public sealed class CurrentUserAccessDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<CurrentUserRoleDto> Roles { get; set; } = Array.Empty<CurrentUserRoleDto>();
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}

public sealed class CurrentUserRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}
