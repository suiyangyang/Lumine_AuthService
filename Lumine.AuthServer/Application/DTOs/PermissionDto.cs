namespace Lumine.AuthServer.Application.DTOs
{
    public class PermissionDto
    {
        public Guid Id { get; set; }
        public string PermissionName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}