using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using System.Text.Json;

namespace Lumine.AuthServer.Api.Auth
{
    public class CustomAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly Microsoft.AspNetCore.Authorization.Policy.AuthorizationMiddlewareResultHandler _defaultHandler = new();

        public async Task HandleAsync(
            RequestDelegate next,
            HttpContext context,
            AuthorizationPolicy policy,
            PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Forbidden)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";

                var requiredPermission = authorizeResult.AuthorizationFailure?.FailedRequirements
                    .OfType<PermissionRequirement>()
                    .Select(item => item.Permission)
                    .FirstOrDefault();

                var response = new AuthorizationErrorResponse
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                    Error = "forbidden",
                    Message = requiredPermission == null
                        ? "当前账号没有访问该资源的权限。"
                        : $"当前账号缺少权限：{requiredPermission}。",
                    Path = context.Request.Path,
                    RequiredPermission = requiredPermission
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
                return;
            }

            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}