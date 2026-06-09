namespace Lumine.AuthServer.Infrastructure.Services
{
    public static class OidcScopes
    {
        public const string OpenId = "openid";
        public const string Profile = "profile";
        public const string Email = "email";
        public const string Roles = "roles";
        public const string Permissions = "permissions";

        public static readonly string[] SupportedScopes =
        [
            OpenId,
            Profile,
            Email,
            Roles,
            Permissions
        ];
    }
}