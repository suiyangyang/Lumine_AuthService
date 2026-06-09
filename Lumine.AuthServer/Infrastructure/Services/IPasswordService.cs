using Lumine.AuthServer.Domain.Entities;

namespace Lumine.AuthServer.Infrastructure.Services
{
    public interface IPasswordService
    {
        string HashPassword(User user, string password);

        bool VerifyPassword(User user, string password, out bool needsRehash);
    }
}