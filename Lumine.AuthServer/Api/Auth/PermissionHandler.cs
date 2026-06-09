using Lumine.AuthServer.Domain.Abstractions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Lumine.AuthServer.Api.Auth
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IUserRepository _userRepo;

        public PermissionHandler(IUserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var userId)) return;

            var has = await _userRepo.UserHasPermissionAsync(userId, requirement.Permission);
            if (has) context.Succeed(requirement);
        }
    }
}
