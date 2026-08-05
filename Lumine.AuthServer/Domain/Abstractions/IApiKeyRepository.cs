using Lumine.AuthServer.Domain.Entities;
using Lumine.SeedWork.Abstractions;

namespace Lumine.AuthServer.Domain.Abstractions;

public interface IApiKeyRepository : IRepository<ApiKey>
{
    Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ApiKey?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken cancellationToken = default);
}
