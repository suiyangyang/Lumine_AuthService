using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Lumine.AuthServer.Api.Auth
{
    public class PermissionSwaggerOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var methodPermissions = context.MethodInfo
                .GetCustomAttributes(true)
                .OfType<PermissionAttribute>();

            var controllerPermissions = context.MethodInfo.DeclaringType?
                .GetCustomAttributes(true)
                .OfType<PermissionAttribute>()
                ?? Enumerable.Empty<PermissionAttribute>();

            var permissions = methodPermissions
                .Concat(controllerPermissions)
                .Select(item => item.Permission)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (permissions.Count == 0)
            {
                return;
            }

            var permissionText = string.Join(", ", permissions.Select(item => $"`{item}`"));
            var description = $"所需权限：{permissionText}";

            operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                ? description
                : $"{operation.Description}\n\n{description}";

            var permissionArray = new OpenApiArray();
            foreach (var permission in permissions)
            {
                permissionArray.Add(new OpenApiString(permission));
            }

            operation.Extensions["x-required-permissions"] = permissionArray;

            operation.Responses.TryAdd("401", new OpenApiResponse
            {
                Description = "未认证：缺少或无效的 Bearer Token"
            });

            operation.Responses.TryAdd("403", new OpenApiResponse
            {
                Description = $"无权限：需要权限 {permissionText}"
            });
        }
    }
}