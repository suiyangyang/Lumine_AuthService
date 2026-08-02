using System.ComponentModel.DataAnnotations.Schema;
using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;

namespace Lumine.AuthServer.Domain.Entities
{
    public class PortalMenu : Entity, IAggregateRoot
    {
        private const char PermissionSeparator = '\n';

        public Guid Id { get; private set; }
        public string Key { get; private set; } = string.Empty;
        public string Title { get; private set; } = string.Empty;
        public string Section { get; private set; } = string.Empty;
        public string Route { get; private set; } = string.Empty;
        public string IconKey { get; private set; } = string.Empty;
        public int DisplayOrder { get; private set; }
        public bool IsEnabled { get; private set; }
        public bool IsImplemented { get; private set; }
        public string RequiredPermissionsValue { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; private set; }

        [NotMapped]
        public IReadOnlyList<string> RequiredPermissions => RequiredPermissionsValue
            .Split(PermissionSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        protected PortalMenu()
        {
        }

        public PortalMenu(
            Guid id,
            string key,
            string title,
            string section,
            string route,
            string iconKey,
            int displayOrder,
            bool isEnabled,
            bool isImplemented,
            IEnumerable<string> requiredPermissions,
            string description,
            DateTime updatedAtUtc)
        {
            Id = id;
            Key = NormalizeRequired(key, nameof(key), 64);
            ApplyDefinition(title, section, route, iconKey, displayOrder, isEnabled, isImplemented, requiredPermissions, description, updatedAtUtc);
        }

        public void UpdateConfiguration(
            string title,
            string section,
            bool isEnabled,
            IEnumerable<string> requiredPermissions,
            string description,
            DateTime updatedAtUtc)
        {
            Title = NormalizeRequired(title, nameof(title), 128);
            Section = NormalizeRequired(section, nameof(section), 64);
            IsEnabled = isEnabled;
            RequiredPermissionsValue = NormalizePermissions(requiredPermissions);
            Description = NormalizeOptional(description, 512);
            UpdatedAtUtc = updatedAtUtc;
        }

        public void SetDisplayOrder(int displayOrder, DateTime updatedAtUtc)
        {
            DisplayOrder = displayOrder > 0
                ? displayOrder
                : throw new ArgumentOutOfRangeException(nameof(displayOrder));
            UpdatedAtUtc = updatedAtUtc;
        }

        public void SynchronizeRegistration(string route, string iconKey, bool isImplemented)
        {
            Route = NormalizeRequired(route, nameof(route), 256);
            IconKey = NormalizeRequired(iconKey, nameof(iconKey), 64);
            IsImplemented = isImplemented;
        }

        public void ResetConfiguration(
            string title,
            string section,
            string route,
            string iconKey,
            int displayOrder,
            bool isEnabled,
            bool isImplemented,
            IEnumerable<string> requiredPermissions,
            string description,
            DateTime updatedAtUtc)
        {
            ApplyDefinition(title, section, route, iconKey, displayOrder, isEnabled, isImplemented, requiredPermissions, description, updatedAtUtc);
        }

        private void ApplyDefinition(
            string title,
            string section,
            string route,
            string iconKey,
            int displayOrder,
            bool isEnabled,
            bool isImplemented,
            IEnumerable<string> requiredPermissions,
            string description,
            DateTime updatedAtUtc)
        {
            Title = NormalizeRequired(title, nameof(title), 128);
            Section = NormalizeRequired(section, nameof(section), 64);
            Route = NormalizeRequired(route, nameof(route), 256);
            IconKey = NormalizeRequired(iconKey, nameof(iconKey), 64);
            DisplayOrder = displayOrder > 0
                ? displayOrder
                : throw new ArgumentOutOfRangeException(nameof(displayOrder));
            IsEnabled = isEnabled;
            IsImplemented = isImplemented;
            RequiredPermissionsValue = NormalizePermissions(requiredPermissions);
            Description = NormalizeOptional(description, 512);
            UpdatedAtUtc = updatedAtUtc;
        }

        private static string NormalizePermissions(IEnumerable<string>? permissions)
        {
            var normalized = permissions?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];

            var serialized = string.Join(PermissionSeparator, normalized);
            return serialized.Length <= 2048
                ? serialized
                : throw new ArgumentException("菜单绑定的权限内容过长。", nameof(permissions));
        }

        private static string NormalizeRequired(string? value, string parameterName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{parameterName} is required.", parameterName);
            }

            var normalized = value.Trim();
            return normalized.Length <= maxLength
                ? normalized
                : throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
        }

        private static string NormalizeOptional(string? value, int maxLength)
        {
            var normalized = value?.Trim() ?? string.Empty;
            return normalized.Length <= maxLength
                ? normalized
                : throw new ArgumentException($"Value cannot exceed {maxLength} characters.", nameof(value));
        }
    }
}
