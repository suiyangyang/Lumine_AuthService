using Lumine.AuthServer.Domain.Entities;
using Lumine.SeedWork.Abstractions;

namespace Lumine.AuthServer.Domain.Abstractions
{
    public interface IRefreshTokenRepository : IRepository<RefreshToken>
    {
        Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    }
}
