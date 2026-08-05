# ReportMind 接入 Lumine AuthService 与 Docker 部署

> 状态：Current
> 适用范围：ReportMind 独立容器、Lumine AuthService 本地开发和权限联调

## 目标架构

ReportMind 不再保存或校验员工密码。登录请求由 ReportMind Server 转发到 Lumine AuthService，Lumine 返回签名的 JWT；ReportMind 使用 Lumine 发布的 JWKS 验证 JWT，并依据 `permission` claim 保护业务接口。

```text
ReportMind Browser/Desktop
        |
        | POST /api/auth/login
        v
ReportMind Server  ----->  Lumine AuthService
        |                         |
        | JWT + permission        | MySQL
        v                         v
/api/test/permission       lumine-auth-mysql-dev
/api/reports/upload
```

当前联调权限：

| 权限 | 用途 |
| --- | --- |
| `reportmind.test` | `GET /api/test/permission` |
| `reportmind.reports.upload` | `POST /api/reports/upload` |

当前联调账号由 Lumine 种子配置创建：

- 用户名：`reportmind-test`
- 角色：`ReportMindUser`
- 密码：只从 `LUMINE_AUTH_REPORTMIND_USER_PASSWORD` 读取，不写入仓库

## 一、配置 Lumine AuthService

在 `Lumine_AuthService` 根目录准备本地环境文件：

```powershell
Copy-Item .env.example .env
```

`.env` 至少需要填写：

```text
LUMINE_AUTH_MYSQL_DATABASE=Lumine.AuthDb
LUMINE_AUTH_MYSQL_USER=<mysql-user>
LUMINE_AUTH_MYSQL_PASSWORD=<mysql-password>
LUMINE_AUTH_MYSQL_ROOT_PASSWORD=<root-password>
LUMINE_AUTH_ADMIN_PASSWORD=<admin-password>
LUMINE_AUTH_REPORTMIND_USER_PASSWORD=<reportmind-test-password>
LUMINE_AUTH_REPORTMIND_DEFAULT_USER_PASSWORD=<migrated-reportmind-user-password>
```

认证服务的 `Lumine.AuthServer/appsettings.json` 已包含以下当前有效种子数据：

- `reportmind.test`
- `reportmind.reports.upload`
- `ReportMindUser`
- `reportmind-test`
- ReportMind 配置中的用户迁移后也由 `SeedData:Users` 写入 Auth 数据库，ReportMind 登录页要求用户直接输入用户名

如果需要修改权限名，必须同时修改 ReportMind 的 `ReportMind:Auth:TestPermission`、`ReportMind:Auth:UploadPermission` 和接口策略配置。

启动 MySQL、执行迁移和种子：

```powershell
.\scripts\init-local-db.ps1
```

仅启动 MySQL：

```powershell
.\scripts\init-local-db.ps1 -SkipBootstrap
```

本地开发时，AuthServer 会从 `.env` 自动组成默认连接串：

```text
server=localhost;port=3307;database=<database>;user=<user>;password=<password>;
```

默认服务地址为 `http://localhost:13255`。如果本机端口被系统保留，可临时用其他端口启动验证，但 JWT 的 `Oidc:Issuer` 仍须与 ReportMind 的 `ReportMind:Auth:Issuer` 一致。

## 二、ReportMind Server 配置

ReportMind Server 的 `ReportMind:Auth` 配置如下：

```json
{
  "ReportMind": {
    "Auth": {
      "BaseUrl": "http://localhost:13255",
      "Issuer": "http://localhost:13255",
      "JwksUrl": "http://localhost:13255/jwks.json",
      "ClientId": "reportmind",
      "LoginScope": "openid profile email roles permissions",
      "UploadPermission": "reportmind.reports.upload",
      "TestPermission": "reportmind.test"
    }
  }
}
```

字段说明：

- `BaseUrl`：ReportMind Server 调用 Lumine 登录接口的地址。
- `Issuer`：JWT 的 `iss`，必须与 Lumine `Oidc:Issuer` 完全一致。
- `JwksUrl`：ReportMind 容器访问 Lumine 公钥的地址；建议显式配置，避免容器内无法访问 JWT issuer 中的 `localhost`。
- `ClientId`：ReportMind 在 Lumine 中注册的 OIDC 客户端标识。ReportMind 登录请求会携带该值，Lumine 会校验客户端存在且启用，并将其写入 JWT 的 `aud`；ReportMind 会校验 JWT 的 `iss`、`aud`、签名和有效期。
- `LoginScope`：建议保留 `roles permissions`，便于客户端显示权限；access token 的权限 claim 由 Lumine 统一签发。
- `UploadPermission`、`TestPermission`：分别绑定上传接口和测试接口的本地授权策略。

当前 ReportMind 使用 `POST /api/auth/login` 的客户端化简化登录入口；`ClientId` 由 ReportMind Server 配置注入，不由用户填写。标准 `/connect/authorize` 授权码跳转仍按 OIDC 客户端的回调地址和 PKCE 规则单独处理。

ReportMind 不再配置员工列表，也不提供用户列表读取功能。登录页由用户直接输入 Auth 用户名；ReportMind Server 使用已注册的 OIDC 客户端配置向 Lumine 发起登录请求。上传时服务端只接受与 JWT 当前用户一致的用户名。

## 三、ReportMind Docker Desktop 配置

在 `E:\Work\Code\Tools\ReportMind` 根目录准备 `.env`：

```powershell
Copy-Item .env.example .env
```

当 Lumine AuthService 运行在 Windows 主机上、ReportMind 运行在 Docker Desktop 容器中时，使用：

```text
REPORTMIND_AUTH_BASE_URL=http://host.docker.internal:13255
REPORTMIND_AUTH_ISSUER=http://localhost:13255
REPORTMIND_AUTH_JWKS_URL=http://host.docker.internal:13255/jwks.json
REPORTMIND_AUTH_CLIENT_ID=reportmind
REPORTMIND_AUTH_UPLOAD_PERMISSION=reportmind.reports.upload
REPORTMIND_AUTH_TEST_PERMISSION=reportmind.test
```

仓库中的 `docker-compose.windows.yml` 已将这些变量映射为 .NET 配置，并添加 `host.docker.internal:host-gateway`。它运行的是独立的 ReportMind Server 容器，不会把 Lumine 或 MySQL 打包进 ReportMind 镜像。

准备宿主机目录：

```powershell
New-Item -ItemType Directory -Force E:\docker-data\ReportMind\config,E:\docker-data\ReportMind\WeeklyReports,E:\docker-data\ReportMind\WorkPlans,E:\docker-data\ReportMind\Logs
```

构建并启动：

```powershell
docker build -f .\ReportMind.Server\Dockerfile -t reportmind-server .
docker compose -f .\docker-compose.windows.yml up -d
```

查看日志：

```powershell
docker compose -f .\docker-compose.windows.yml logs -f reportmind-server
```

容器端口：

- 宿主机 `10545`
- 容器 `8080`
- 健康检查：`http://localhost:10545/health`
- Browser/API：`http://localhost:10545/`

配置文件挂载位置：

- 宿主机：`E:\docker-data\ReportMind\config\appsettings.json`
- 容器：`/app/config/appsettings.json`

如果使用挂载配置覆盖认证设置，至少保留：

```json
{
  "ReportMind": {
    "Auth": {
      "BaseUrl": "http://host.docker.internal:13255",
      "Issuer": "http://localhost:13255",
      "JwksUrl": "http://host.docker.internal:13255/jwks.json",
      "ClientId": "reportmind",
      "UploadPermission": "reportmind.reports.upload",
      "TestPermission": "reportmind.test"
    }
  }
}
```

## 四、接口验证

### 1. 登录

```powershell
$login = Invoke-RestMethod `
  -Uri http://localhost:10545/api/auth/login `
  -Method Post `
  -ContentType 'application/json' `
  -Body (@{ name = 'reportmind-test'; password = '<从环境变量读取>' } | ConvertTo-Json)
$token = $login.accessToken
```

ReportMind 不保存该密码，也不生成自己的 token；`accessToken` 直接来自 Lumine。

### 2. 无令牌访问测试接口应为 401

```powershell
Invoke-WebRequest http://localhost:10545/api/test/permission
```

### 3. 带令牌访问测试接口应为 200

```powershell
Invoke-RestMethod `
  -Uri http://localhost:10545/api/test/permission `
  -Headers @{ Authorization = "Bearer $token" }
```

成功响应会包含：

- `success: true`
- `userName: reportmind-test`
- `requiredPermission: reportmind.test`
- `permissions` 中包含 `reportmind.test` 和 `reportmind.reports.upload`

### 4. 上传接口

`POST /api/reports/upload` 使用 `multipart/form-data`，要求：

- Bearer token
- `reportmind.reports.upload` 权限
- `file` 字段
- `employeeName` 字段
- `.xlsx`、`.xls` 或 `.txt` 文件
- 最大 100 MB

PowerShell 示例：

```powershell
$form = @{
  file = Get-Item .\sample.txt
  employeeName = 'reportmind-test'
}
Invoke-RestMethod `
  -Uri http://localhost:10545/api/reports/upload `
  -Method Post `
  -Headers @{ Authorization = "Bearer $token" } `
  -Form $form
```

## 五、故障排查

| 现象 | 检查项 |
| --- | --- |
| 登录返回 503 | ReportMind 容器能否访问 `BaseUrl/api/auth/login`；Windows Docker 使用 `host.docker.internal` |
| 登录返回 401 | Lumine 数据库是否已执行种子；用户名和密码是否对应；检查 `reportmind-test` 是否存在 |
| 测试接口返回 401 | Bearer token 是否传递；`Issuer` 是否与 token 的 `iss` 一致 |
| 测试接口返回 403 | Lumine 用户是否有 `reportmind.test`；ReportMind `TestPermission` 是否一致 |
| 接口返回签名错误 | 检查 `JwksUrl` 是否从 ReportMind 容器可访问，且与签发 token 的 AuthServer 使用同一实例 |
| 上传返回 403 | Lumine 用户是否有 `reportmind.reports.upload`；ReportMind `UploadPermission` 是否一致 |
| 上传返回 400 | 检查字段名、文件扩展名、Content-Type 和 100 MB 限制 |

## 六、当前验证结论

已验证以下闭环：

1. Docker Desktop MySQL `lumine-auth-mysql-dev` 在 `localhost:3307` 健康运行。
2. Lumine 数据库中存在 `reportmind-test`、`ReportMindUser` 和两个 ReportMind 权限。
3. ReportMind 通过 Lumine 登录取得 access token。
4. `reportmind-test` 携带该 token 访问 `/api/test/permission` 返回 200。
5. 测试接口响应确认 token 中包含 `reportmind.test` 与 `reportmind.reports.upload`。

## NAS Docker 当前联调地址

- AuthService：`http://192.168.1.100:13255`
- ReportMind：`http://192.168.1.100:10545`
- ReportMind 容器通过 Docker 网络访问 AuthService：`http://lumine-authserver:8080`
- ReportMind 使用的 JWKS 地址：`http://lumine-authserver:8080/jwks.json`
- JWT issuer：`http://192.168.1.100:13255`
- MySQL 通过 `auth-mysql:3306` 提供给 AuthServer；宿主机管理端口为 `3307`。

NAS 当前配置已将 ReportMind 配置中的员工用户迁移到 Lumine AuthService，活动 ReportMind 配置不再保存 `Employees` 用户列表或员工密码。迁移用户统一使用部署时配置的 Auth 密码；ReportMind 的 `POST /api/auth/login` 负责转发登录，`GET /api/test/permission` 继续使用 `reportmind.test` 权限保护。
