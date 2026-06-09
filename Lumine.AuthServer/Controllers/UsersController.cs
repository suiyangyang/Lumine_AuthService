using Lumine.AuthServer.Application.DTOs;
using Lumine.AuthServer.Api.Auth;
using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.AuthServer.Infrastructure;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IPasswordService _passwordService;

        public UsersController(
            AuthDbContext dbContext,
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IPasswordService passwordService)
        {
            _dbContext = dbContext;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _passwordService = passwordService;
        }

        [HttpGet]
        [Permission("users.view")]
        public async Task<ActionResult<PagedResultDto<UserDto>>> GetAll([FromQuery] string? keyword, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            var query = QueryUsers(_dbContext.Users.AsNoTracking());
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                query = query.Where(user => user.UserName.Contains(normalizedKeyword)
                    || user.Email.Contains(normalizedKeyword)
                    || (user.NickName != null && user.NickName.Contains(normalizedKeyword)));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var users = await query
                .OrderBy(user => user.UserName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new PagedResultDto<UserDto>
            {
                Items = users.Select(MapUser).ToArray(),
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            });
        }

        [HttpGet("{id:guid}")]
        [Permission("users.view")]
        public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var user = await QueryUsers(_dbContext.Users.AsNoTracking())
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            return user == null ? NotFound() : Ok(MapUser(user));
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetCurrentUser(CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { message = "无法识别当前登录用户。" });
            }

            var user = await QueryUsers(_dbContext.Users.AsNoTracking())
                .FirstOrDefaultAsync(item => item.Id == currentUserId.Value, cancellationToken);

            return user == null ? NotFound() : Ok(MapUser(user));
        }

        [HttpPut("me")]
        public async Task<ActionResult<UserDto>> UpdateCurrentUser([FromBody] UpdateCurrentUserProfileRequest request, CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { message = "无法识别当前登录用户。" });
            }

            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "用户名和邮箱不能为空。" });
            }

            var user = await _dbContext.Users
                .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
                .ThenInclude(item => item.RolePermissions)
                .ThenInclude(item => item.Permission)
                .FirstOrDefaultAsync(item => item.Id == currentUserId.Value, cancellationToken);

            if (user == null)
            {
                return NotFound();
            }

            var normalizedUserName = request.UserName.Trim();
            var existingUser = await _userRepository.GetByUserNameAsync(normalizedUserName);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                return Conflict(new { message = $"用户 '{normalizedUserName}' 已存在。" });
            }

            user.UpdateProfile(normalizedUserName, request.Email.Trim(), request.NickName?.Trim(), request.PhoneNumber?.Trim(), user.IsActive);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(MapUser(user));
        }

        [HttpPost("me/change-password")]
        public async Task<IActionResult> ChangeCurrentUserPassword([FromBody] ChangeCurrentUserPasswordRequest request, CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { message = "无法识别当前登录用户。" });
            }

            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "当前密码和新密码不能为空。" });
            }

            if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            {
                return BadRequest(new { message = "新密码不能与当前密码相同。" });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == currentUserId.Value, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            if (!_passwordService.VerifyPassword(user, request.CurrentPassword, out _))
            {
                return BadRequest(new { message = "当前密码不正确。" });
            }

            user.SetPasswordHash(_passwordService.HashPassword(user, request.NewPassword.Trim()));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "密码修改成功。" });
        }

        [HttpPost]
        [Permission("users.manage")]
        public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("用户名、邮箱和密码不能为空。");
            }

            var normalizedUserName = request.UserName.Trim();
            var existingUser = await _userRepository.GetByUserNameAsync(normalizedUserName);
            if (existingUser != null)
            {
                return Conflict($"用户 '{normalizedUserName}' 已存在。");
            }

            var roleIds = request.RoleIds?.Distinct().ToList() ?? new List<Guid>();
            var roles = roleIds.Count == 0
                ? new List<Role>()
                : await _roleRepository.GetByIdsAsync(roleIds);

            if (roles.Count != roleIds.Count)
            {
                return BadRequest("存在无效的角色 Id。");
            }

            var user = new User(Guid.NewGuid(), normalizedUserName, request.Email.Trim(), string.Empty);
            user.UpdateProfile(normalizedUserName, request.Email.Trim(), request.NickName, request.PhoneNumber, request.IsActive);
            user.SetPasswordHash(_passwordService.HashPassword(user, request.Password));

            foreach (var role in roles)
            {
                user.AssignRole(role);
            }

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var createdUser = await QueryUsers(_dbContext.Users.AsNoTracking())
                .FirstAsync(item => item.Id == user.Id, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, MapUser(createdUser));
        }

        [HttpPut("{id:guid}")]
        [Permission("users.manage")]
        public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
                .ThenInclude(item => item.RolePermissions)
                .ThenInclude(item => item.Permission)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (user == null)
            {
                return NotFound();
            }

            var normalizedUserName = request.UserName.Trim();
            var existingUser = await _userRepository.GetByUserNameAsync(normalizedUserName);
            if (existingUser != null && existingUser.Id != id)
            {
                return Conflict($"用户 '{normalizedUserName}' 已存在。");
            }

            user.UpdateProfile(normalizedUserName, request.Email.Trim(), request.NickName, request.PhoneNumber, request.IsActive);

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.SetPasswordHash(_passwordService.HashPassword(user, request.Password));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(MapUser(user));
        }

        [HttpPut("{id:guid}/status")]
        [Permission("users.manage")]
        public async Task<ActionResult<UserDto>> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
                .ThenInclude(item => item.RolePermissions)
                .ThenInclude(item => item.Permission)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (user == null)
            {
                return NotFound();
            }

            user.SetActive(request.IsActive);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(MapUser(user));
        }

        [HttpPost("{id:guid}/reset-password")]
        [Permission("users.manage")]
        public async Task<ActionResult<UserDto>> ResetPassword(Guid id, [FromBody] ResetUserPasswordRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("新密码不能为空。");
            }

            var user = await _dbContext.Users
                .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
                .ThenInclude(item => item.RolePermissions)
                .ThenInclude(item => item.Permission)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (user == null)
            {
                return NotFound();
            }

            user.SetPasswordHash(_passwordService.HashPassword(user, request.Password.Trim()));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(MapUser(user));
        }

        [HttpDelete("{id:guid}")]
        [Permission("users.manage")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [HttpPut("{id:guid}/roles")]
        [Permission("users.manage")]
        public async Task<ActionResult<UserDto>> ReplaceRoles(Guid id, [FromBody] ReplaceUserRolesRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
                .ThenInclude(item => item.RolePermissions)
                .ThenInclude(item => item.Permission)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (user == null)
            {
                return NotFound();
            }

            var roleIds = request.RoleIds.Distinct().ToList();
            var roles = roleIds.Count == 0
                ? new List<Role>()
                : await _dbContext.Roles.Where(role => roleIds.Contains(role.Id)).ToListAsync(cancellationToken);

            if (roles.Count != roleIds.Count)
            {
                return BadRequest("存在无效的角色 Id。");
            }

            var currentRoleIds = user.UserRoles.Select(item => item.RoleId).ToList();
            foreach (var roleId in currentRoleIds.Except(roleIds).ToList())
            {
                user.RemoveRole(roleId);
            }

            foreach (var role in roles)
            {
                user.AssignRole(role);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var updatedUser = await QueryUsers(_dbContext.Users.AsNoTracking())
                .FirstAsync(item => item.Id == id, cancellationToken);

            return Ok(MapUser(updatedUser));
        }

        [HttpPost("{id:guid}/roles/{roleId:guid}")]
        [Permission("users.manage")]
        public async Task<ActionResult<UserDto>> AddRole(Guid id, Guid roleId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .Include(item => item.UserRoles)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role == null)
            {
                return NotFound($"角色 '{roleId}' 不存在。");
            }

            user.AssignRole(role);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var updatedUser = await QueryUsers(_dbContext.Users.AsNoTracking())
                .FirstAsync(item => item.Id == id, cancellationToken);

            return Ok(MapUser(updatedUser));
        }

        [HttpDelete("{id:guid}/roles/{roleId:guid}")]
        [Permission("users.manage")]
        public async Task<ActionResult<UserDto>> RemoveRole(Guid id, Guid roleId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .Include(item => item.UserRoles)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            user.RemoveRole(roleId);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var updatedUser = await QueryUsers(_dbContext.Users.AsNoTracking())
                .FirstAsync(item => item.Id == id, cancellationToken);

            return Ok(MapUser(updatedUser));
        }

        private static IQueryable<User> QueryUsers(IQueryable<User> queryable)
        {
            return queryable
                .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .ThenInclude(role => role.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission);
        }

        private static UserDto MapUser(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                NickName = user.NickName,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                CreatedAtUtc = user.CreatedAtUtc,
                LastLoginAtUtc = user.LastLoginAtUtc,
                Roles = user.UserRoles
                    .Select(userRole => userRole.Role)
                    .Where(role => role != null)
                    .OrderBy(role => role.Name)
                    .Select(MapRole)
                    .ToList()
            };
        }

        private static RoleDto MapRole(Role role)
        {
            return new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Permissions = role.RolePermissions
                    .Where(item => item.Permission != null)
                    .OrderBy(item => item.Permission.PermissionName)
                    .Select(item => new PermissionDto
                    {
                        Id = item.PermissionId,
                        PermissionName = item.Permission.PermissionName,
                        DisplayName = item.Permission.DisplayName
                    })
                    .ToList()
            };
        }

        private Guid? GetCurrentUserId()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(userIdValue, out var userId) ? userId : null;
        }
    }

    public record CreateUserRequest(
        string UserName,
        string Email,
        string Password,
        string? NickName,
        string? PhoneNumber,
        bool IsActive,
        List<Guid>? RoleIds);

    public record UpdateUserRequest(
        string UserName,
        string Email,
        string? NickName,
        string? PhoneNumber,
        bool IsActive,
        string? Password);

    public record UpdateUserStatusRequest(bool IsActive);

    public record ResetUserPasswordRequest(string Password);

    public record ReplaceUserRolesRequest(List<Guid> RoleIds);

    public record UpdateCurrentUserProfileRequest(string UserName, string Email, string? NickName, string? PhoneNumber);

    public record ChangeCurrentUserPasswordRequest(string CurrentPassword, string NewPassword);
}