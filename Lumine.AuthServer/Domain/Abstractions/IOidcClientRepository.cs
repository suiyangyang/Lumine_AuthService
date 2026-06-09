using Lumine.AuthServer.Domain.Entities;
using Lumine.SeedWork.Abstractions;

namespace Lumine.AuthServer.Domain.Abstractions
{
    public interface IOidcClientRepository : IRepository<OidcClient>
    {
        Task<OidcClient?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

        Task<OidcClient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
