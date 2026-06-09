using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;

namespace Lumine.AuthServer.Domain.Entities
{
    public class AuditLogEntry : Entity, IAggregateRoot
    {
        public Guid Id { get; private set; }
        public string Category { get; private set; } = string.Empty;
        public string Action { get; private set; } = string.Empty;
        public string Actor { get; private set; } = string.Empty;
        public string Target { get; private set; } = string.Empty;
        public string Outcome { get; private set; } = string.Empty;
        public string Details { get; private set; } = string.Empty;
        public string? IpAddress { get; private set; }
        public DateTime OccurredAtUtc { get; private set; }

        protected AuditLogEntry()
        {
        }

        public AuditLogEntry(
            Guid id,
            string category,
            string action,
            string actor,
            string target,
            string outcome,
            string? details,
            string? ipAddress,
            DateTime occurredAtUtc)
        {
            Id = id;
            Category = Normalize(category, nameof(category), 64);
            Action = Normalize(action, nameof(action), 64);
            Actor = Normalize(actor, nameof(actor), 128);
            Target = Normalize(target, nameof(target), 256);
            Outcome = Normalize(outcome, nameof(outcome), 64);
            Details = NormalizeOptional(details, 2048);
            IpAddress = NormalizeOptional(ipAddress, 64);
            OccurredAtUtc = occurredAtUtc;
        }

        private static string Normalize(string? value, string parameterName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{parameterName} is required.", parameterName);
            }

            var normalized = value.Trim();
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }

        private static string NormalizeOptional(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim();
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }
    }
}
