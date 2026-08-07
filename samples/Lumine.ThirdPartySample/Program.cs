using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var authServerBaseUrl = builder.Configuration["AuthServer:BaseUrl"] ?? "http://localhost:14255";
var issuer = builder.Configuration["AuthServer:Issuer"] ?? authServerBaseUrl;
var clientId = builder.Configuration["AuthServer:ClientId"] ?? "lumine-demo-client";
var operationPermission = builder.Configuration["ThirdParty:OperationPermission"] ?? "users.view";

builder.Services.AddHttpClient("auth-server", client =>
{
    client.BaseAddress = new Uri(authServerBaseUrl.TrimEnd('/') + "/");
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MetadataAddress = issuer.TrimEnd('/') + "/.well-known/openid-configuration";
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer.TrimEnd('/'),
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "username",
            RoleClaimType = "roles"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("internal-operation", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasPermission(context.User, operationPermission)));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "Lumine Third-Party Sample",
    login = "POST /login",
    protectedOperation = "GET /api/internal-operation"
}));

app.MapPost("/login", async (
    LoginRequest request,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "userName 和 password 不能为空。" });
    }

    var authClient = httpClientFactory.CreateClient("auth-server");
    using var loginResponse = await authClient.PostAsJsonAsync("api/auth/login", new
    {
        userName = request.UserName,
        password = request.Password,
        scope = "openid profile email roles permissions",
        clientId,
        nonce = Guid.NewGuid().ToString("N")
    }, cancellationToken);

    if (!loginResponse.IsSuccessStatusCode)
    {
        return Results.StatusCode((int)loginResponse.StatusCode);
    }

    var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
    if (login == null || string.IsNullOrWhiteSpace(login.AccessToken))
    {
        return Results.Problem("AuthServer 未返回 access_token。", statusCode: StatusCodes.Status502BadGateway);
    }

    using var permissionRequest = new HttpRequestMessage(HttpMethod.Get, "api/auth/me/permissions");
    permissionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    using var permissionResponse = await authClient.SendAsync(permissionRequest, cancellationToken);
    if (!permissionResponse.IsSuccessStatusCode)
    {
        return Results.Problem("无法从 AuthServer 获取当前用户权限。", statusCode: StatusCodes.Status502BadGateway);
    }

    var access = await permissionResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
    return Results.Ok(new
    {
        access_token = login.AccessToken,
        token_type = login.TokenType,
        expires_in = login.ExpiresIn,
        current_user_access = access
    });
});

app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
{
    userId = user.FindFirst("sub")?.Value,
    userName = user.FindFirst("username")?.Value,
    roles = user.FindAll("roles").Select(claim => claim.Value).ToArray(),
    permissions = GetPermissions(user)
})).RequireAuthorization();

app.MapGet("/api/internal-operation", (ClaimsPrincipal user) => Results.Ok(new
{
    message = "第三方服务操作鉴权成功。",
    operationPermission,
    userId = user.FindFirst("sub")?.Value,
    userName = user.FindFirst("username")?.Value
})).RequireAuthorization("internal-operation");

app.MapPost("/api/remote-permission-check", async (
    PermissionCheckRequest request,
    HttpContext httpContext,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.PermissionName))
    {
        return Results.BadRequest(new { message = "permissionName 不能为空。" });
    }

    var token = httpContext.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.Unauthorized();
    }

    var authClient = httpClientFactory.CreateClient("auth-server");
    using var checkRequest = new HttpRequestMessage(HttpMethod.Post, "api/auth/check-permission")
    {
        Content = JsonContent.Create(new { permissionName = request.PermissionName.Trim() })
    };
    checkRequest.Headers.TryAddWithoutValidation("Authorization", token);
    using var response = await authClient.SendAsync(checkRequest, cancellationToken);
    var payload = await response.Content.ReadAsStringAsync(cancellationToken);
    return Results.Content(payload, response.Content.Headers.ContentType?.ToString() ?? "application/json", statusCode: (int)response.StatusCode);
}).RequireAuthorization();

app.Run();

static bool HasPermission(ClaimsPrincipal user, string requiredPermission)
{
    return GetPermissions(user).Contains(requiredPermission, StringComparer.OrdinalIgnoreCase);
}

static string[] GetPermissions(ClaimsPrincipal user)
{
    return user.Claims
        .Where(claim => claim.Type is "permissions" or "permission")
        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(permission => permission)
        .ToArray();
}

public sealed record LoginRequest(string UserName, string Password);

public sealed record PermissionCheckRequest(string PermissionName);

public sealed class LoginResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
