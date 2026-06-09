using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;

namespace Lumine.AuthServer.Domain.Entities
{
    public class RolePermission : Entity, IAggregateRoot
    {
        public Guid RoleId { get; private set; }
        public Guid PermissionId { get; private set; }
        public string PermissionName { get; private set; } = null!;

        // Navigation properties
        public Role Role { get; private set; } = null!;
        public Permission Permission { get; private set; } = null!;

        protected RolePermission() { }
        public RolePermission(Guid roleId, Guid permissionId, string permissionName)
        {
            RoleId = roleId;
            PermissionId = permissionId;
            PermissionName = permissionName;
        }
    }
}
