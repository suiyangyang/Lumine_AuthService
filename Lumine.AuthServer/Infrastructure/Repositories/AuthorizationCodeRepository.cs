using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.Services.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Infrastructure.Repositories
{
    public class AuthorizationCodeRepository : EfRepository<AuthorizationCode, AuthDbContext>, IAuthorizationCodeRepository
    {
        private readonly AuthDbContext _dbContext;

        public AuthorizationCodeRepository(AuthDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<AuthorizationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return _dbContext.AuthorizationCodes
                .Include(item => item.Client)
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
        }
    }
}
