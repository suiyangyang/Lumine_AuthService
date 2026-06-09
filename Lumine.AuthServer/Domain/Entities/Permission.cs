using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;

namespace Lumine.AuthServer.Domain.Entities
{
    public class Permission : Entity, IAggregateRoot
    {
        public Guid Id { get; private set; }
        public string PermissionName { get; private set; } = null!;
        public string DisplayName { get; private set; } = null!;

        protected Permission() { }

        public Permission(Guid id, string permissionName, string? displayName = null)
        {
            Id = id;
            PermissionName = permissionName ?? throw new ArgumentNullException(nameof(permissionName));
            DisplayName = displayName ?? permissionName;
        }

        public void Update(string permissionName, string? displayName = null)
        {
            PermissionName = string.IsNullOrWhiteSpace(permissionName)
                ? throw new ArgumentException("Permission name is required.", nameof(permissionName))
                : permissionName.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? PermissionName : displayName.Trim();
        }
    }
}
