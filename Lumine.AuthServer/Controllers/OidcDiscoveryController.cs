using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    public class OidcDiscoveryController : ControllerBase
    {
        private readonly IOidcSigningCredentialsService _signingCredentialsService;

        public OidcDiscoveryController(IOidcSigningCredentialsService signingCredentialsService)
        {
            _signingCredentialsService = signingCredentialsService;
        }

        [HttpGet("/.well-known/openid-configuration")]
        public IActionResult GetConfiguration()
        {
            var issuer = _signingCredentialsService.Issuer;

            return Ok(new
            {
                issuer,
                authorization_endpoint = $"{issuer}/connect/authorize",
                token_endpoint = $"{issuer}/connect/token",
                jwks_uri = $"{issuer}/jwks.json",
                userinfo_endpoint = $"{issuer}/connect/userinfo",
                scopes_supported = OidcScopes.SupportedScopes,
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { SecurityAlgorithms.RsaSha256 },
                code_challenge_methods_supported = new[] { "S256", "plain" },
                claims_supported = new[]
                {
                    JwtRegisteredClaimNames.Sub,
                    JwtRegisteredClaimNames.PreferredUsername,
                    JwtRegisteredClaimNames.Name,
                    JwtRegisteredClaimNames.Nickname,
                    JwtRegisteredClaimNames.Email,
                    "email_verified",
                    "roles",
                    "permissions"
                },
                grant_types_supported = new[] { "authorization_code", "refresh_token" },
                response_types_supported = new[] { "code" },
                token_endpoint_auth_methods_supported = new[] { "none" }
            });
        }

        [HttpGet("/jwks.json")]
        public IActionResult GetJwks()
        {
            return Ok(new
            {
                keys = new[]
                {
                    _signingCredentialsService.GetJsonWebKey()
                }
            });
        }
    }
}