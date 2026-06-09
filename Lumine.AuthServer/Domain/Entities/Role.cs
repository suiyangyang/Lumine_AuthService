using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lumine.AuthServer.Domain.Entities
{
    public class Role : Entity, IAggregateRoot
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = null!;

        public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();
        [NotMapped]
        public IEnumerable<RolePermission> Permissions => RolePermissions;

        // Navigation properties
        public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

        protected Role() { }

        public Role(Guid id, string name)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public void Rename(string name)
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Role name is required.", nameof(name))
                : name.Trim();
        }

        public void AddPermission(Permission permission)
        {
            if (permission == null) throw new ArgumentNullException(nameof(permission));
            if (RolePermissions.Any(p => p.PermissionId == permission.Id)) return;
            RolePermissions.Add(new RolePermission(Id, permission.Id, permission.PermissionName));
        }

        public void RemovePermission(Guid permissionId)
        {
            var found = RolePermissions.FirstOrDefault(p => p.PermissionId == permissionId);
            if (found != null) RolePermissions.Remove(found);
        }
    }
}
