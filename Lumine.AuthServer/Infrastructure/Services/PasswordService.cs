using Lumine.AuthServer.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Lumine.AuthServer.Infrastructure.Services
{
    public class PasswordService : IPasswordService
    {
        private readonly IPasswordHasher<User> _passwordHasher;

        public PasswordService(IPasswordHasher<User> passwordHasher)
        {
            _passwordHasher = passwordHasher;
        }

        public string HashPassword(User user, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be empty.", nameof(password));
            }

            return _passwordHasher.HashPassword(user, password);
        }

        public bool VerifyPassword(User user, string password, out bool needsRehash)
        {
            needsRehash = false;

            if (string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Success)
            {
                return true;
            }

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                needsRehash = true;
                return true;
            }

            if (string.Equals(user.PasswordHash, password, StringComparison.Ordinal))
            {
                needsRehash = true;
                return true;
            }

            return false;
        }
    }
}