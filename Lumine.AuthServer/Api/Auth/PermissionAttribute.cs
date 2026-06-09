using Microsoft.AspNetCore.Authorization;

namespace Lumine.AuthServer.Api.Auth
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class PermissionAttribute : AuthorizeAttribute
    {
        public string Permission { get; }

        public PermissionAttribute(string permission)
        {
            Permission = permission;
            Policy = PermissionPolicyProvider.PolicyPrefix + permission;
        }
    }
}
