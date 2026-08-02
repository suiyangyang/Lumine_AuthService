# Lumine 第三方服务最小示例

这个示例模拟公司内部业务系统：它调用 Lumine AuthServer 登录，读取当前用户的角色和权限，再用 AuthServer 发布的 JWKS 验证 JWT，并保护一个业务操作。

## 运行

先确保 AuthServer 已启动并完成本地数据库初始化，然后在仓库根目录执行：

```powershell
dotnet run --project .\samples\Lumine.ThirdPartySample\Lumine.ThirdPartySample.csproj --urls http://localhost:5222
```

默认使用 `admin` 账号；密码以 `.env` 中的 `LUMINE_AUTH_ADMIN_PASSWORD` 为准。

## 验证登录、权限和操作鉴权

```powershell
$login = Invoke-RestMethod http://localhost:5222/login -Method Post -ContentType 'application/json' -Body (@{
  userName = 'admin'
  password = 'change-me-local'
} | ConvertTo-Json)

$headers = @{ Authorization = "Bearer $($login.access_token)" }
Invoke-RestMethod http://localhost:5222/api/me -Headers $headers
Invoke-RestMethod http://localhost:5222/api/internal-operation -Headers $headers
```

`/login` 的响应中 `current_user_access.permissions` 来自 `GET /api/auth/me/permissions`；`/api/internal-operation` 则由第三方服务依据 JWT 中的 `permissions`/`permission` claim 做本地操作鉴权。

还可以验证权限中心的实时远程检查：

```powershell
Invoke-RestMethod http://localhost:5222/api/remote-permission-check -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
  permissionName = 'users.view'
} | ConvertTo-Json)
```

将 `appsettings.json` 中 `ThirdParty:OperationPermission` 改成当前用户没有的权限后，`/api/internal-operation` 会返回 `403`；移除 `Authorization` 头则返回 `401`。

## 接入边界

示例为了便于本地验收使用 `POST /api/auth/login`，真实业务系统应优先使用 `/connect/authorize` 的授权码 + PKCE 流程。AuthServer 登录成功后会建立 `lumine-auth-session` Cookie，已登录员工可继续访问授权端点完成第三方系统登录。
