using Lumine.AuthServer.Domain.Entities;

namespace Lumine.AuthServer.Infrastructure.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly AuthDbContext _dbContext;

        public AuditLogService(AuthDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task WriteAsync(
            string category,
            string action,
            string actor,
            string target,
            string outcome,
            string? details = null,
            string? ipAddress = null,
            DateTime? occurredAtUtc = null,
            CancellationToken cancellationToken = default)
        {
            var entry = new AuditLogEntry(
                Guid.NewGuid(),
                category,
                action,
                actor,
                target,
                outcome,
                details,
                ipAddress,
                occurredAtUtc ?? DateTime.UtcNow);

            await _dbContext.AuditLogEntries.AddAsync(entry, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
