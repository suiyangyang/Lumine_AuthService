namespace Lumine.AuthServer.Api.Auth
{
    public class AuthorizationErrorResponse
    {
        public int StatusCode { get; init; }

        public string Error { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public string? RequiredPermission { get; init; }

        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }
}