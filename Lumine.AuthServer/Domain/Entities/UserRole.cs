using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;

namespace Lumine.AuthServer.Domain.Entities
{
    public class UserRole : Entity, IAggregateRoot
    {
        public Guid UserId { get; private set; }
        public Guid RoleId { get; private set; }

        // Navigation properties
        public User User { get; private set; } = null!;
        public Role Role { get; private set; } = null!;

        protected UserRole() { }
        public UserRole(Guid userId, Guid roleId)
        {
            UserId = userId;
            RoleId = roleId;
        }
    }
}
