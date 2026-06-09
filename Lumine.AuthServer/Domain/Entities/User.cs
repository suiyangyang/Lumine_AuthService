using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;
using System.Data;

namespace Lumine.AuthServer.Domain.Entities
{
    public class User : Entity, IAggregateRoot
    {
        public Guid Id { get; private set; }
        public string UserName { get; private set; } = null!;
        public string Email { get; private set; } = null!;
        public string? NickName { get; private set; }
        public string PasswordHash { get; private set; } = null!;
        public string? PhoneNumber { get; private set; }
        public bool IsActive { get; private set; } = true;
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime? LastLoginAtUtc { get; private set; }

        public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

        protected User() { }

        public User(Guid id, string userName, string email, string passwordHash)
        {
            Id = id;
            UserName = userName ?? throw new ArgumentNullException(nameof(userName));
            Email = email;
            PasswordHash = passwordHash;
            CreatedAtUtc = DateTime.UtcNow;
        }

        public void UpdateProfile(string userName, string email, string? nickName, string? phoneNumber, bool isActive)
        {
            UserName = string.IsNullOrWhiteSpace(userName)
                ? throw new ArgumentException("User name is required.", nameof(userName))
                : userName.Trim();
            Email = string.IsNullOrWhiteSpace(email)
                ? throw new ArgumentException("Email is required.", nameof(email))
                : email.Trim();
            NickName = string.IsNullOrWhiteSpace(nickName) ? null : nickName.Trim();
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
            IsActive = isActive;
        }

        public void SetPasswordHash(string passwordHash)
        {
            PasswordHash = string.IsNullOrWhiteSpace(passwordHash)
                ? throw new ArgumentException("Password hash is required.", nameof(passwordHash))
                : passwordHash;
        }

        public void SetActive(bool isActive)
        {
            IsActive = isActive;
        }

        public void MarkLoginSucceeded(DateTime? loginAtUtc = null)
        {
            LastLoginAtUtc = loginAtUtc ?? DateTime.UtcNow;
        }

        public void AssignRole(Role role)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            if (UserRoles.Any(r => r.RoleId == role.Id)) return;
            UserRoles.Add(new UserRole(Id, role.Id));
        }

        public void RemoveRole(Guid roleId)
        {
            var found = UserRoles.FirstOrDefault(r => r.RoleId == roleId);
            if (found != null) UserRoles.Remove(found);
        }

        public bool HasPermission(string permissionName, IEnumerable<Role> rolesWithPermissions)
        {
            // Helper to check against provided roles with permissions (avoid DB in entity)
            var roleIds = UserRoles.Select(r => r.RoleId).ToHashSet();
            return rolesWithPermissions
                .Where(r => roleIds.Contains(r.Id))
                .SelectMany(r => r.Permissions)
                .Any(p => string.Equals(p.PermissionName, permissionName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
