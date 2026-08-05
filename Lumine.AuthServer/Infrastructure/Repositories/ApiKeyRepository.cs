using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Lumine.Services.Core.Infrastructure.Persistence;

namespace Lumine.AuthServer.Infrastructure.Repositories;

public sealed class ApiKeyRepository : EfRepository<ApiKey, AuthDbContext>, IApiKeyRepository
{
    private readonly AuthDbContext _dbContext;

    public ApiKeyRepository(AuthDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApiKeys
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.Status == ApiKeyStatuses.Active)
            .OrderByDescending(item => item.CreateTime)
            .ToListAsync(cancellationToken);
    }

    public Task<ApiKey?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.ApiKeys
            .FirstOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
    }

    public Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return _dbContext.ApiKeys
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.KeyHash == keyHash, cancellationToken);
    }
}
