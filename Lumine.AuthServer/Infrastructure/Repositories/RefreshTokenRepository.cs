using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.Services.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Infrastructure.Repositories
{
    public class RefreshTokenRepository : EfRepository<RefreshToken, AuthDbContext>, IRefreshTokenRepository
    {
        private readonly AuthDbContext _dbContext;

        public RefreshTokenRepository(AuthDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            return _dbContext.RefreshTokens
                .Include(item => item.Client)
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.Token == token, cancellationToken);
        }
    }
}
