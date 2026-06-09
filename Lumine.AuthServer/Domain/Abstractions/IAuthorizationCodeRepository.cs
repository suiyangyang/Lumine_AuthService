using Lumine.AuthServer.Domain.Entities;
using Lumine.SeedWork.Abstractions;

namespace Lumine.AuthServer.Domain.Abstractions
{
    public interface IAuthorizationCodeRepository : IRepository<AuthorizationCode>
    {
        Task<AuthorizationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    }
}
