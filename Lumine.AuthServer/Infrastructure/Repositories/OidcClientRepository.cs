using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.Services.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Infrastructure.Repositories
{
    public class OidcClientRepository : EfRepository<OidcClient, AuthDbContext>, IOidcClientRepository
    {
        private readonly AuthDbContext _dbContext;

        public OidcClientRepository(AuthDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<OidcClient?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
        {
            return _dbContext.OidcClients
                .Include(item => item.RedirectUris)
                .FirstOrDefaultAsync(item => item.ClientId == clientId, cancellationToken);
        }

        public Task<OidcClient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _dbContext.OidcClients
                .Include(item => item.RedirectUris)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        }
    }
}
