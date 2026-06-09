namespace Lumine.AuthServer.Infrastructure.Services
{
    public interface IAuditLogService
    {
        Task WriteAsync(
            string category,
            string action,
            string actor,
            string target,
            string outcome,
            string? details = null,
            string? ipAddress = null,
            DateTime? occurredAtUtc = null,
            CancellationToken cancellationToken = default);
    }
}
