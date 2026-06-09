# Lumine AuthService 部署说明

> 状态：Current
> 更新时间：2026-06-09
> 适用范围：`Lumine.AuthServer` 与 `Lumine.AuthPortal.Browser` 的本地、Docker 与发布部署

本文档描述当前仓库已经落地的部署方式，而不是历史施工过程。

当前部署形态如下：

- `Lumine.AuthServer` 是唯一后端进程，负责 API、OIDC 端点和静态资源托管。
- `Lumine.AuthPortal.Browser` 在构建或发布时产出 Browser 静态资源，并被整理到服务端 `wwwroot`。
- 浏览器访问后端地址时，可同时拿到管理后台前端和认证接口。
- 本地开发默认地址为 `http://localhost:5115`。
- Docker 调试默认通过 `ASPNETCORE_HTTP_PORTS=8080` 暴露服务。

## 当前部署结构

| 组件 | 作用 | 关键文件 |
| --- | --- | --- |
| `Lumine.AuthServer` | 托管 API、OIDC、Swagger、前端静态资源 | `Lumine.AuthServer/Program.cs` |
| `Lumine.AuthPortal.Browser` | 生成 Browser 前端资源 | `Lumine.AuthPortal/Lumine.AuthPortal.Browser/Lumine.AuthPortal.Browser.csproj` |
| `Lumine.AuthServer.csproj` | 构建后整理 Browser 资源到服务端输出目录 | `Lumine.AuthServer/Lumine.AuthServer.csproj` |
| `Lumine.AuthServer/Dockerfile` | 构建服务镜像并复制 Browser 资源 | `Lumine.AuthServer/Dockerfile` |

## 本地运行

先准备数据库与环境变量，再启动后端：

```powershell
Copy-Item .env.example .env
.\scripts\init-local-db.ps1
dotnet run --project .\Lumine.AuthServer\Lumine.AuthServer.csproj
```

默认开发入口：

- 服务根地址：`http://localhost:5115`
- Swagger：`http://localhost:5115/swagger`
- OIDC Discovery：`http://localhost:5115/.well-known/openid-configuration`
- Portal 前端：`http://localhost:5115/`

如果只想单独运行 Browser 前端调试页面：

```powershell
dotnet run --project .\Lumine.AuthPortal\Lumine.AuthPortal.Browser\Lumine.AuthPortal.Browser.csproj
```

## 构建与发布

构建后端时，`Lumine.AuthServer.csproj` 会在 `Build` 和 `Publish` 后把 Browser 的 `wwwroot`、`_framework`、`_content` 资源复制到服务端输出目录。

常用命令：

```powershell
dotnet build .\Lumine_AuthService.sln
```

```powershell
dotnet publish .\Lumine.AuthServer\Lumine.AuthServer.csproj -c Release
```

发布结果中的 `wwwroot` 应包含：

- `index.html`
- `_framework/`
- `_content/`

如果这些资源缺失，优先检查：

- `Lumine.AuthPortal.Browser` 是否已成功构建
- `Lumine.AuthServer.csproj` 中的 Browser 复制目标是否仍存在
- `Program.cs` 中的静态文件根路径是否被改动

## Docker 部署

当前 Dockerfile 采用单容器部署：

1. 在 `sdk:10.0` 镜像中构建后端和 Browser 前端
2. 将 Browser 资源复制到 `/app/browser-publish`
3. 发布 `Lumine.AuthServer`
4. 在最终 `aspnet:10.0` 镜像中仅运行 `Lumine.AuthServer.dll`

构建镜像示例：

```powershell
docker build -f .\Lumine.AuthServer\Dockerfile -t lumine-authserver:local .
```

运行镜像示例：

```powershell
docker run --rm --env-file .env -p 8080:8080 lumine-authserver:local
```

容器内关键约束：

- 服务监听 `8080`
- 通过项目文件的 `DockerfileRunEnvironmentFiles` 读取仓库根目录 `.env`
- 前端与 API 同源，不需要额外网关即可完成登录和 OIDC 联调

## 部署前检查

建议至少确认以下项目：

1. `.env` 中数据库与管理员密码已配置。
2. `SeedData:AdminPassword` 不为空。
3. `Oidc:Issuer` 与实际访问地址一致。
4. 默认客户端 `lumine-demo-client` 的回调地址与联调环境一致。
5. 发布输出或容器内的 `wwwroot/_framework` 可正常访问。

## 相关文档

- 本地数据库：`docs/deployment/LocalDb.md`
- OIDC 当前实现：`docs/auth/OAuth2.1-OIDC-Avalonia改造说明.md`
- 第三方接入：`docs/integrations/第三方服务接入说明.md`
- 历史施工记录：`docs/archive/deployment/AvaloniaBrowserAspNetCore一体化部署施工记录.md`
- 历史排障记录：`docs/archive/deployment/AvaloniaBrowserContainerDebugTroubleshooting-2026-06.md`
