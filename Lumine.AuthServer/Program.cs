using Lumine.AuthServer.Api.Auth;
using Lumine.AuthServer.Application.Services;
using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Infrastructure;
using Lumine.AuthServer.Infrastructure.Repositories;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var contentRoot = builder.Environment.ContentRootPath ?? string.Empty;
var deployedWwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var defaultWwwroot = ResolveDefaultWwwroot(contentRoot, deployedWwwroot);
var browserAssets = ResolveBrowserAssets(contentRoot, defaultWwwroot);

if (!string.IsNullOrWhiteSpace(browserAssets.PrimaryWebRoot))
{
    builder.Environment.WebRootPath = browserAssets.PrimaryWebRoot;
}

var oidcOptions = builder.Configuration.GetSection(OidcOptions.SectionName).Get<OidcOptions>() ?? new OidcOptions();
var oidcSigningCredentialsService = new OidcSigningCredentialsService(oidcOptions);

// Configuration (appsettings.json should contain Oidc:Issuer and optional Oidc:PrivateKeyPem)

/* docker-compose.yml 
 services:
    lumine-authserver:
    image: your-image-name
    environment:
      - ConnectionStrings__DefaultConnection=server=mysql;port=3306;database=LumineIdentityDb;user=root;password=Lumine_#@!;
        # 其他配置项...
 */
var connectionString = ResolveMySqlConnectionString(builder.Configuration.GetConnectionString("DefaultConnection"));

services.AddDbContext<AuthDbContext>(opt =>
    opt.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(9, 4, 0)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)
    ));

services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<IRoleRepository, RoleRepository>();
services.AddScoped<IPermissionRepository, PermissionRepository>();
services.AddScoped<IUserRoleRepository, UserRoleRepository>();
services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
services.AddScoped<IOidcClientRepository, OidcClientRepository>();
services.AddScoped<IAuthorizationCodeRepository, AuthorizationCodeRepository>();
services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
services.AddScoped<IPasswordHasher<Lumine.AuthServer.Domain.Entities.User>, PasswordHasher<Lumine.AuthServer.Domain.Entities.User>>();
services.AddScoped<IPasswordService, PasswordService>();
services.AddScoped<IOidcService, OidcService>();
services.AddScoped<IAuthenticationAppService, AuthenticationAppService>();
services.AddSingleton<IOidcSigningCredentialsService>(oidcSigningCredentialsService);
services.Configure<OidcOptions>(builder.Configuration.GetSection(OidcOptions.SectionName));
services.Configure<AuthSeedOptions>(builder.Configuration.GetSection(AuthSeedOptions.SectionName));
services.AddScoped<AuthDataSeeder>();
services.AddCors(options =>
{
    options.AddPolicy("PortalDevelopment", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(Program)));

// Auth
var issuer = oidcSigningCredentialsService.Issuer;

services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        IssuerSigningKey = oidcSigningCredentialsService.ValidationKey
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = new AuthorizationErrorResponse
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Error = "unauthorized",
                Message = "请先登录，或提供有效的 Bearer Token。",
                Path = context.Request.Path
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    };
});

//services.AddMigrationManager();
services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
services.AddScoped<IAuthorizationHandler, PermissionHandler>();
services.AddSingleton<IAuthorizationMiddlewareResultHandler, CustomAuthorizationMiddlewareResultHandler>();

services.AddAuthorization();
services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Lumine AuthServer API",
        Version = "v1",
        Description = "认证服务及后台用户/角色/权限管理接口"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "输入 Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            securityScheme,
            Array.Empty<string>()
        }
    });

    options.OperationFilter<PermissionSwaggerOperationFilter>();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await dbContext.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<AuthDataSeeder>();
    await seeder.SeedAsync();
}

//using (var scope = app.Services.CreateScope())
//{
//    var migrationManager = scope.ServiceProvider.GetRequiredService<MigrationManager>();
//    var dbContext = scope.ServiceProvider.GetRequiredService<EFContext>();

//    await migrationManager.RunAsync(dbContext, app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted);
//}

if (app.Environment.IsDevelopment())
{
    app.UseCors("PortalDevelopment");
    app.UseSwagger();
    app.UseSwaggerUI();
}

var staticFileProvider = CreateStaticFileProvider(browserAssets, defaultWwwroot);
var contentTypeProvider = CreateContentTypeProvider();

if (staticFileProvider is not null)
{
    var defaultFilesOptions = new DefaultFilesOptions
    {
        FileProvider = staticFileProvider
    };
    defaultFilesOptions.DefaultFileNames.Clear();
    defaultFilesOptions.DefaultFileNames.Add("index.html");

    app.UseDefaultFiles(defaultFilesOptions);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = staticFileProvider,
        ContentTypeProvider = contentTypeProvider,
        ServeUnknownFileTypes = true,
        DefaultContentType = "application/octet-stream",
        OnPrepareResponse = context =>
        {
            if (ShouldDisableCache(context.Context.Request.Path))
            {
                ApplyNoCacheHeaders(context.Context.Response.Headers);
            }
        }
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/config", (HttpRequest request) =>
{
    var envPort = ResolvePortOverride();
    var scheme = string.IsNullOrWhiteSpace(request.Scheme) ? "http" : request.Scheme;
    var port = request.Host.Port?.ToString() ?? envPort ?? "5115";
    var host = request.Host.HasValue ? request.Host.Value : $"localhost:{port}";
    var apiBase = $"{scheme}://{host}";
    return Results.Ok(new { apiBase, port });
});
app.MapControllers();

if (staticFileProvider is not null && HasBrowserEntryAssets(browserAssets.PrimaryWebRoot))
{
    app.MapFallback(async context =>
    {
        if (IsServerRoute(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var indexFile = staticFileProvider.GetFileInfo("index.html");
        if (!indexFile.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyNoCacheHeaders(context.Response.Headers);

        if (!string.IsNullOrWhiteSpace(indexFile.PhysicalPath))
        {
            await context.Response.SendFileAsync(indexFile.PhysicalPath);
            return;
        }

        await using var stream = indexFile.CreateReadStream();
        await stream.CopyToAsync(context.Response.Body);
    });
}

app.Run();

static string ResolveMySqlConnectionString(string? rawConnectionString)
{
    if (string.IsNullOrWhiteSpace(rawConnectionString))
    {
        throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    var builder = new MySqlConnectionStringBuilder(rawConnectionString);
    var runningInContainer = string.Equals(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        "true",
        StringComparison.OrdinalIgnoreCase);

    if (runningInContainer && IsLocalMySqlHost(builder.Server))
    {
        builder.Server = "host.docker.internal";
    }

    return builder.ConnectionString;
}

static bool IsLocalMySqlHost(string? server)
{
    if (string.IsNullOrWhiteSpace(server))
    {
        return false;
    }

    return string.Equals(server, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(server, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(server, "::1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(server, "local", StringComparison.OrdinalIgnoreCase);
}

static FileExtensionContentTypeProvider CreateContentTypeProvider()
{
    var provider = new FileExtensionContentTypeProvider();
    provider.Mappings[".dat"] = "application/octet-stream";
    provider.Mappings[".dll"] = "application/octet-stream";
    provider.Mappings[".pdb"] = "application/octet-stream";
    provider.Mappings[".symbols"] = "application/octet-stream";
    provider.Mappings[".wasm"] = "application/wasm";
    provider.Mappings[".br"] = "application/brotli";
    return provider;
}

static string ResolveDefaultWwwroot(string contentRoot, string deployedWwwroot)
{
    if (HasBrowserEntryAssets(deployedWwwroot) || Directory.Exists(deployedWwwroot))
    {
        return deployedWwwroot;
    }

    return Path.Combine(contentRoot, "wwwroot");
}

static BrowserAssets ResolveBrowserAssets(string contentRoot, string defaultWwwroot)
{
    if (HasBrowserRuntimeAssets(defaultWwwroot))
    {
        return new BrowserAssets(defaultWwwroot, new[] { defaultWwwroot });
    }

    var possibleBrowserRoots = new[]
    {
        Path.GetFullPath(Path.Combine(contentRoot, "..", "Lumine.AuthPortal", "Lumine.AuthPortal.Browser")),
        Path.GetFullPath(Path.Combine(contentRoot, "Lumine.AuthPortal", "Lumine.AuthPortal.Browser")),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Lumine.AuthPortal", "Lumine.AuthPortal.Browser"))
    };

    foreach (var browserRoot in possibleBrowserRoots.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        var sourceWwwroot = Path.Combine(browserRoot, "wwwroot");
        var buildRoots = new[]
        {
            Path.Combine(browserRoot, "bin", "Debug", "net10.0-browser", "wwwroot"),
            Path.Combine(browserRoot, "bin", "Release", "net10.0-browser", "wwwroot")
        };

        var runtimeRoot = buildRoots.FirstOrDefault(HasBrowserRuntimeRuntimeAssets);
        if (!string.IsNullOrWhiteSpace(runtimeRoot) && Directory.Exists(sourceWwwroot))
        {
            return new BrowserAssets(sourceWwwroot, new[] { sourceWwwroot, runtimeRoot });
        }

        var publishRoots = new[]
        {
            Path.Combine(browserRoot, "bin", "Debug", "net10.0-browser", "publish"),
            Path.Combine(browserRoot, "bin", "Release", "net10.0-browser", "publish"),
            Path.Combine(browserRoot, "bin", "Debug", "net10.0-browser", "publish", "wwwroot"),
            Path.Combine(browserRoot, "bin", "Release", "net10.0-browser", "publish", "wwwroot"),
            Path.Combine(browserRoot, "bin", "Debug", "net10.0-browser", "browser-wasm", "publish", "wwwroot"),
            Path.Combine(browserRoot, "bin", "Release", "net10.0-browser", "browser-wasm", "publish", "wwwroot")
        };

        var publishRoot = publishRoots.FirstOrDefault(HasBrowserRuntimeAssets);
        if (!string.IsNullOrWhiteSpace(publishRoot) && Directory.Exists(sourceWwwroot))
        {
            return new BrowserAssets(sourceWwwroot, new[] { sourceWwwroot, publishRoot });
        }

        if (!string.IsNullOrWhiteSpace(publishRoot))
        {
            return new BrowserAssets(publishRoot, new[] { publishRoot });
        }
    }

    return new BrowserAssets(defaultWwwroot, Directory.Exists(defaultWwwroot) ? new[] { defaultWwwroot } : Array.Empty<string>());
}

static IFileProvider? CreateStaticFileProvider(BrowserAssets browserAssets, string fallbackRoot)
{
    var providerRoots = browserAssets.ProviderRoots
        .Where(Directory.Exists)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (providerRoots.Length == 0)
    {
        return Directory.Exists(fallbackRoot) ? new PhysicalFileProvider(fallbackRoot) : null;
    }

    if (providerRoots.Length == 1)
    {
        return new PhysicalFileProvider(providerRoots[0]);
    }

    return new CompositeFileProvider(providerRoots.Select(path => new PhysicalFileProvider(path)).ToArray<IFileProvider>());
}

static bool HasBrowserRuntimeAssets(string webRoot)
{
    return Directory.Exists(webRoot)
        && File.Exists(Path.Combine(webRoot, "index.html"))
        && File.Exists(Path.Combine(webRoot, "main.js"))
        && File.Exists(Path.Combine(webRoot, "_framework", "dotnet.js"));
}

static bool HasBrowserRuntimeRuntimeAssets(string webRoot)
{
    return Directory.Exists(webRoot)
        && File.Exists(Path.Combine(webRoot, "_framework", "dotnet.js"));
}

static bool HasBrowserEntryAssets(string webRoot)
{
    return Directory.Exists(webRoot)
        && File.Exists(Path.Combine(webRoot, "index.html"))
        && File.Exists(Path.Combine(webRoot, "main.js"));
}

static bool ShouldDisableCache(PathString requestPath)
{
    var value = requestPath.Value ?? string.Empty;
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    return value.Equals("/", StringComparison.OrdinalIgnoreCase)
        || value.Equals("/index.html", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith("/main.js", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith("/api-base.js", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith("/_framework/dotnet.js", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith("/_framework/dotnet.runtime.js", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith("/_framework/dotnet.native.js", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith("/_framework/blazor.boot.json", StringComparison.OrdinalIgnoreCase);
}

static void ApplyNoCacheHeaders(IHeaderDictionary headers)
{
    headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
    headers["Pragma"] = "no-cache";
    headers["Expires"] = "0";
}

static bool IsServerRoute(PathString requestPath)
{
    return requestPath.StartsWithSegments("/api")
        || requestPath.StartsWithSegments("/connect")
        || requestPath.StartsWithSegments("/.well-known")
        || requestPath.StartsWithSegments("/swagger");
}

static string? ResolvePortOverride()
{
    var portValue = Environment.GetEnvironmentVariable("PORT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");

    return string.IsNullOrWhiteSpace(portValue)
        ? null
        : portValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}

internal sealed record BrowserAssets(string PrimaryWebRoot, string[] ProviderRoots);