using Lumine.AuthServer.Application.DTOs;
using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Lumine.AuthServer.Controllers;

[ApiController]
[Authorize]
[Route("api/api-keys")]
public sealed class ApiKeysController : ControllerBase
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IOidcClientRepository _clientRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogService _auditLogService;

    public ApiKeysController(
        IApiKeyRepository apiKeyRepository,
        IOidcClientRepository clientRepository,
        IUserRepository userRepository,
        IAuditLogService auditLogService)
    {
        _apiKeyRepository = apiKeyRepository;
        _clientRepository = clientRepository;
        _userRepository = userRepository;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error = "invalid_token", message = "Token 中缺少有效的用户标识。" });
        }

        var items = await _apiKeyRepository.GetByUserIdAsync(userId.Value, cancellationToken);
        return Ok(items.Select(Map).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<CreatedApiKeyDto>> Create([FromBody] CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error = "invalid_token", message = "Token 中缺少有效的用户标识。" });
        }

        if (string.IsNullOrWhiteSpace(request.ClientId) || !ApiKeyClientIds.IsSupported(request.ClientId.Trim()))
        {
            return BadRequest(new { error = "invalid_client", message = $"API-KEY 暂只支持 client_id={ApiKeyClientIds.LabChromKnowledge}。" });
        }

        var clientId = request.ClientId.Trim();
        var client = await _clientRepository.GetByClientIdAsync(clientId, cancellationToken);
        if (client == null || !client.IsActive)
        {
            return BadRequest(new { error = "invalid_client", message = "客户端不存在或已停用。" });
        }

        var expireTime = request.ExpireTime?.ToUniversalTime();
        if (expireTime.HasValue && expireTime.Value <= DateTime.UtcNow)
        {
            return BadRequest(new { error = "invalid_request", message = "ExpireTime 必须晚于当前时间。" });
        }

        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { error = "invalid_token", message = "当前用户不存在或已停用。" });
        }

        var apiKey = ApiKey.Create(Guid.NewGuid(), user.Id, client.ClientId, expireTime, out var plainTextKey);
        await _apiKeyRepository.AddAsync(apiKey, cancellationToken);
        await _apiKeyRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync(
            "认证",
            "创建 API-KEY",
            user.UserName,
            client.ClientId,
            "成功",
            $"KeyPrefix: {apiKey.KeyPrefix}",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            apiKey.CreateTime,
            cancellationToken);

        return Created("/api/api-keys", new CreatedApiKeyDto
        {
            Id = apiKey.Id,
            UserId = apiKey.UserId,
            ClientId = apiKey.ClientId,
            KeyPrefix = apiKey.KeyPrefix,
            KeySuffix = apiKey.KeySuffix,
            CreateTime = apiKey.CreateTime,
            ExpireTime = apiKey.ExpireTime,
            Status = apiKey.Status,
            LastUsedTime = apiKey.LastUsedTime,
            PlainTextKey = plainTextKey,
            ApiKey = plainTextKey
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error = "invalid_token", message = "Token 中缺少有效的用户标识。" });
        }

        var apiKey = await _apiKeyRepository.GetByIdAsync(userId.Value, id, cancellationToken);
        if (apiKey == null)
        {
            return NotFound();
        }

        apiKey.Revoke();
        _apiKeyRepository.Update(apiKey);
        await _apiKeyRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync(
            "认证",
            "删除 API-KEY",
            User.Identity?.Name ?? userId.Value.ToString(),
            apiKey.ClientId,
            "成功",
            $"KeyPrefix: {apiKey.KeyPrefix}",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            DateTime.UtcNow,
            cancellationToken);

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static ApiKeyDto Map(ApiKey item)
    {
        return new ApiKeyDto
        {
            Id = item.Id,
            UserId = item.UserId,
            ClientId = item.ClientId,
            KeyPrefix = item.KeyPrefix,
            PlainTextKey = item.PlainTextKey,
            KeySuffix = item.KeySuffix,
            CreateTime = item.CreateTime,
            ExpireTime = item.ExpireTime,
            Status = item.Status,
            LastUsedTime = item.LastUsedTime
        };
    }
}

public sealed record CreateApiKeyRequest(string ClientId, DateTime? ExpireTime = null);
