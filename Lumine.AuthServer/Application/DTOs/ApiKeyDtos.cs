namespace Lumine.AuthServer.Application.DTOs;

public class ApiKeyDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public DateTime? ExpireTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastUsedTime { get; set; }
}

public sealed class CreatedApiKeyDto : ApiKeyDto
{
    public string ApiKey { get; set; } = string.Empty;
}
