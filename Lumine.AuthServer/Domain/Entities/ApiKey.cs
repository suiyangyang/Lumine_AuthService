using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;
using System.Security.Cryptography;
using System.Text;

namespace Lumine.AuthServer.Domain.Entities;

public sealed class ApiKey : Entity, IAggregateRoot
{
    private const string RandomAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string ClientId { get; private set; } = null!;
    public string KeyPrefix { get; private set; } = null!;
    public string KeyHash { get; private set; } = null!;
    public DateTime CreateTime { get; private set; }
    public DateTime? ExpireTime { get; private set; }
    public string Status { get; private set; } = ApiKeyStatuses.Active;
    public DateTime? LastUsedTime { get; private set; }

    public User? User { get; private set; }

    private ApiKey() { }

    private ApiKey(Guid id, Guid userId, string clientId, string keyPrefix, string keyHash, DateTime createTime, DateTime? expireTime)
    {
        Id = id;
        UserId = userId;
        ClientId = Normalize(clientId, nameof(clientId), 128);
        KeyPrefix = Normalize(keyPrefix, nameof(keyPrefix), 16);
        KeyHash = Normalize(keyHash, nameof(keyHash), 64);
        CreateTime = createTime;
        ExpireTime = expireTime;
    }

    public static ApiKey Create(Guid id, Guid userId, string clientId, DateTime? expireTimeUtc, out string plainTextKey)
    {
        var randomPrefix = string.Concat(Enumerable.Range(0, 6)
            .Select(_ => RandomAlphabet[RandomNumberGenerator.GetInt32(RandomAlphabet.Length)]));
        plainTextKey = $"sk-{randomPrefix}{Guid.NewGuid():N}";
        return new ApiKey(
            id,
            userId,
            clientId,
            plainTextKey[..9],
            ComputeHash(plainTextKey),
            DateTime.UtcNow,
            expireTimeUtc);
    }

    public static string ComputeHash(string plainTextKey)
    {
        if (string.IsNullOrWhiteSpace(plainTextKey))
        {
            throw new ArgumentException("API-KEY 不能为空。", nameof(plainTextKey));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainTextKey.Trim())));
    }

    public bool IsUsable(string clientId, DateTime utcNow)
    {
        return string.Equals(Status, ApiKeyStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ClientId, clientId, StringComparison.OrdinalIgnoreCase)
            && (!ExpireTime.HasValue || ExpireTime.Value > utcNow);
    }

    public void MarkUsed(DateTime utcNow)
    {
        LastUsedTime = utcNow;
    }

    public void Revoke()
    {
        Status = ApiKeyStatuses.Revoked;
    }

    private static string Normalize(string value, string paramName, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} 不能为空。", paramName)
            : value.Trim();
        return normalized.Length > maxLength
            ? throw new ArgumentException($"{paramName} 长度不能超过 {maxLength}。", paramName)
            : normalized;
    }
}

public static class ApiKeyStatuses
{
    public const string Active = "active";
    public const string Revoked = "revoked";
}

public static class ApiKeyClientIds
{
    public const string LabChromKnowledge = "labchrom_knowledge";

    public static bool IsSupported(string clientId)
    {
        return string.Equals(clientId, LabChromKnowledge, StringComparison.OrdinalIgnoreCase);
    }
}
