using Lumine.AuthServer.Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;

namespace Lumine.AuthServer.Infrastructure.Services
{
    public class OidcService : IOidcService
    {
        public IReadOnlyCollection<string> NormalizeScopes(string? scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return Array.Empty<string>();
            }

            var scopes = scope
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var unsupportedScopes = scopes
                .Where(item => !OidcScopes.SupportedScopes.Contains(item, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (unsupportedScopes.Count > 0)
            {
                throw new ArgumentException($"Unsupported scopes: {string.Join(", ", unsupportedScopes)}", nameof(scope));
            }

            return scopes;
        }

        public string CreateAccessToken(User user, IReadOnlyCollection<Role> roles, IReadOnlyCollection<string> scopes, string issuer, SigningCredentials signingCredentials, string? audience, TimeSpan lifetime)
        {
            var claims = BuildAccessClaims(user, roles, scopes);
            return WriteToken(claims, issuer, signingCredentials, audience, lifetime);
        }

        public string? CreateIdToken(User user, IReadOnlyCollection<Role> roles, IReadOnlyCollection<string> scopes, string issuer, SigningCredentials signingCredentials, string? audience, string? nonce, DateTimeOffset authTime, TimeSpan lifetime)
        {
            if (!scopes.Contains(OidcScopes.OpenId, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(JwtRegisteredClaimNames.AuthTime, authTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            if (!string.IsNullOrWhiteSpace(nonce))
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));
            }

            AddScopedIdentityClaims(claims, user, roles, scopes);
            AddScopeClaims(claims, scopes);

            return WriteToken(claims, issuer, signingCredentials, audience, lifetime);
        }

        public IDictionary<string, object> BuildUserInfo(User user, IReadOnlyCollection<Role> roles, IReadOnlyCollection<string> scopes)
        {
            var result = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString()
            };

            if (scopes.Contains(OidcScopes.Profile, StringComparer.OrdinalIgnoreCase))
            {
                result[JwtRegisteredClaimNames.PreferredUsername] = user.UserName;
                result[JwtRegisteredClaimNames.Name] = user.NickName ?? user.UserName;
                if (!string.IsNullOrWhiteSpace(user.NickName))
                {
                    result[JwtRegisteredClaimNames.Nickname] = user.NickName;
                }
            }

            if (scopes.Contains(OidcScopes.Email, StringComparer.OrdinalIgnoreCase))
            {
                result[JwtRegisteredClaimNames.Email] = user.Email;
                result["email_verified"] = false;
            }

            if (scopes.Contains(OidcScopes.Roles, StringComparer.OrdinalIgnoreCase))
            {
                result["roles"] = roles.Select(role => role.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            if (scopes.Contains(OidcScopes.Permissions, StringComparer.OrdinalIgnoreCase))
            {
                result["permissions"] = roles
                    .SelectMany(role => role.RolePermissions)
                    .Select(item => item.PermissionName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return result;
        }

        public string CreateOpaqueToken(int size = 32)
        {
            var bytes = RandomNumberGenerator.GetBytes(size);
            return Base64UrlEncoder.Encode(bytes);
        }

        public IReadOnlyCollection<string> GetScopes(ClaimsPrincipal principal)
        {
            return principal.Claims
                .Where(claim => claim.Type is "scope" or "scp")
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static List<Claim> BuildAccessClaims(User user, IReadOnlyCollection<Role> roles, IReadOnlyCollection<string> scopes)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName),
                new("username", user.UserName),
                new("token_use", "access_token")
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role.Name)));
            claims.AddRange(roles
                .SelectMany(role => role.RolePermissions)
                .Select(item => item.PermissionName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(permission => new Claim("permission", permission)));

            AddScopeClaims(claims, scopes);
            return claims;
        }

        private static void AddScopedIdentityClaims(List<Claim> claims, User user, IReadOnlyCollection<Role> roles, IReadOnlyCollection<string> scopes)
        {
            if (scopes.Contains(OidcScopes.Profile, StringComparer.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.PreferredUsername, user.UserName));
                claims.Add(new Claim(JwtRegisteredClaimNames.Name, user.NickName ?? user.UserName));

                if (!string.IsNullOrWhiteSpace(user.NickName))
                {
                    claims.Add(new Claim(JwtRegisteredClaimNames.Nickname, user.NickName));
                }
            }

            if (scopes.Contains(OidcScopes.Email, StringComparer.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
                claims.Add(new Claim("email_verified", bool.FalseString.ToLowerInvariant(), ClaimValueTypes.Boolean));
            }

            if (scopes.Contains(OidcScopes.Roles, StringComparer.OrdinalIgnoreCase))
            {
                claims.AddRange(roles
                    .Select(role => role.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(role => new Claim("roles", role)));
            }

            if (scopes.Contains(OidcScopes.Permissions, StringComparer.OrdinalIgnoreCase))
            {
                claims.AddRange(roles
                    .SelectMany(role => role.RolePermissions)
                    .Select(item => item.PermissionName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(permission => new Claim("permissions", permission)));
            }
        }

        private static void AddScopeClaims(List<Claim> claims, IReadOnlyCollection<string> scopes)
        {
            foreach (var scope in scopes)
            {
                claims.Add(new Claim("scope", scope));
            }
        }

        private static string WriteToken(IEnumerable<Claim> claims, string issuer, SigningCredentials signingCredentials, string? audience, TimeSpan lifetime)
        {
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: string.IsNullOrWhiteSpace(audience) ? null : audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(lifetime),
                signingCredentials: signingCredentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}